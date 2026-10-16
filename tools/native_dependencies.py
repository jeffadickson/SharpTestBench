"""Record dynamic imports; reject missing or undeclared non-system dependencies."""
import os, re, subprocess, sys
from pathlib import Path

def inspect(native, simulator_hosts=()):
    reports={}
    host_names={name.lower() for name in simulator_hosts}
    for path in sorted(native.iterdir()):
        if path.suffix.lower() not in ('.so','.dylib','.dll','.vpi'):continue
        if sys.platform=='linux':
            output=subprocess.check_output(['ldd',str(path)],env=dict(os.environ,LD_LIBRARY_PATH=str(native)),text=True,stderr=subprocess.STDOUT)
            if 'not found' in output:raise RuntimeError(f'Missing native dependency: {path}\n{output}')
            for resolved in re.findall(r'=> (/\S+)',output):
                if not (Path(resolved).is_relative_to(native) or resolved.startswith(('/lib/','/lib64/','/usr/lib/'))):
                    raise RuntimeError('Unexpected external native dependency: '+resolved)
        elif sys.platform=='darwin':
            output=subprocess.check_output(['otool','-L',str(path)],text=True)
            identifiers=subprocess.check_output(['otool','-D',str(path)],text=True).splitlines()[1:]
            install_ids={line.strip() for line in identifiers}
            for line in output.splitlines()[1:]:
                dependency=line.strip().split(' (')[0]
                if dependency in install_ids:continue  # LC_ID_DYLIB is identity, not an imported dependency.
                if dependency.startswith(('/usr/lib/','/System/Library/')):continue
                if dependency==str(path) or Path(dependency).name==path.name:continue
                if not (native/Path(dependency).name).is_file():raise RuntimeError('Unpackaged native dependency: '+dependency)
        else:
            output=subprocess.check_output([os.environ.get('OBJDUMP', 'objdump'),'-p',str(path)],text=True)
            dependencies=re.findall(r'DLL Name:\s*(\S+)',output)
            if not dependencies:raise RuntimeError('No PE import information for '+str(path))
            for dependency in dependencies:
                lower=dependency.lower()
                if lower in host_names:continue  # Symbols supplied by the explicitly selected simulator executable.
                system=lower in {'kernel32.dll','msvcrt.dll','ucrtbase.dll','advapi32.dll','user32.dll','ws2_32.dll','shell32.dll','ole32.dll','bcrypt.dll','ntdll.dll'} or lower.startswith(('api-ms-win-','ext-ms-win-','vcruntime140','msvcp140'))
                if not system and not any(p.name.lower()==lower for p in native.iterdir()):raise RuntimeError('Unpackaged native dependency: '+dependency)
            output='\n'.join(dependencies)
        reports[path.name]=output
    return {'platform':sys.platform,'imports':reports,'simulator_hosts':sorted(host_names),'system_prerequisites':'OS runtime libraries; Windows VC++ runtime matching the process architecture where imported. .NET 10 and matching Icarus installed separately.'}
