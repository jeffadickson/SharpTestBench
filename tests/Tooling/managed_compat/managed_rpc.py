"""Test-only bindings to production C# contracts; no helper algorithms live here."""
import atexit, json, os, pathlib, queue, subprocess, threading, xml.etree.ElementTree as ET
ROOT=pathlib.Path(__file__).resolve().parents[3]
_process=None
_lock=threading.Lock()
_messages=None
def close():
 global _process
 if _process is not None:
  _process.stdin.close()
  try:_process.wait(timeout=5)
  except subprocess.TimeoutExpired:_process.kill();_process.wait()
  _process=None
atexit.register(close)
def call(module,name,args=(),kwargs=None,maximum=None):
 global _process, _messages
 with _lock:
  if _process is None:
   dll=pathlib.Path(os.environ.get('STB_MANAGED_TEST_API_DLL',str(ROOT/'tools/SharpTestBench.Tools/bin/Release/net10.0/SharpTestBench.Tools.dll')))
   _process=subprocess.Popen(['dotnet',str(dll),'test-api'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,encoding='utf-8')
   _messages=queue.Queue()
   def read_messages(stream, messages):
    for response in stream:messages.put(response)
    messages.put(None)
   threading.Thread(target=read_messages,args=(_process.stdout,_messages),daemon=True).start()
  request=dict(module=module,name=name,args=args,kwargs=kwargs or {})
  if maximum is not None:request['maximumBytes']=maximum
  _process.stdin.write(json.dumps(request,default=lambda value:str(value) if isinstance(value,pathlib.Path) else list(value))+'\n');_process.stdin.flush()
  try:line=_messages.get(timeout=60)
  except queue.Empty:
   _process.kill();_process.wait();raise TimeoutError('Managed test API response timed out')
  if not line:raise RuntimeError('Managed test API exited: '+_process.stderr.read())
  response=json.loads(line)
 if not response['ok']:
  kind=response['kind'];message=response['message']
  if kind=='XmlException':raise ET.ParseError(message)
  if kind=='DecoderFallbackException':raise UnicodeError(message)
  if kind in ('FileNotFoundException','DirectoryNotFoundException'):raise FileNotFoundError(message)
  if kind in ('IOException','UnauthorizedAccessException'):raise OSError(message)
  raise ValueError(message)
 return response['result']
