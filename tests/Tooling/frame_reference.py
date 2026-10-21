"""Independent frame-commit replay; no simulator orchestration."""
from collections import deque
import argparse
import json


def validate(config, plan, metrics):
    def require(value, message):
        if not value: raise ValueError(message)
    require(type(config) is dict and set(config)=={'Depth','Pipeline','OutputFifo'},'configuration fields')
    depth=config['Depth']
    require(type(depth) is int and depth in (4,8,16,32,64),'depth')
    require(type(config['Pipeline']) is int and 0<=config['Pipeline']<=3 and type(config['OutputFifo']) is bool,'pipeline')
    require(type(plan) is dict and set(plan)=={'Frames','Ready'},'plan fields')
    frames,ready=plan['Frames'],plan['Ready']
    require(type(frames) is list and 1<=len(frames)<=32,'frames')
    words=[]
    for frame in frames:
        require(type(frame) is list and 1<=len(frame)<depth,'frame length')
        for index,word in enumerate(frame):
            require(type(word) is dict and set(word)=={'Data','Keep','Last','Id','Dest','User'},'word fields')
            for key,low,high in [('Data',0,65535),('Keep',1,3),('Id',0,15),('Dest',0,15),('User',0,3)]:
                require(type(word[key]) is int and low<=word[key]<=high,'word range')
            require(type(word['Last']) is bool and word['Last']==(index==len(frame)-1),'last boundary')
            words.append((word['Data'],word['Keep'],int(word['Last']),word['Id'],word['Dest'],word['User']))
    require(len(words)<=256 and type(ready) is list and 3<=len(ready)<=4000 and all(type(x) is bool for x in ready),'ready plan')
    require(not ready[0] and len(ready)>=depth+80 and all(ready[-(depth+80):]),'drain')
    require(type(metrics) is dict and set(metrics)=={'trace','accepted','consumed','flushed','stalls','resets','cycles'},'metrics fields')
    trace=metrics['trace'];require(type(trace) is list and len(trace)==len(ready),'trace length')
    assembled=[];committed=deque();held=None;accepted=consumed=stalls=hidden=0
    for cycle,row in enumerate(trace):
        require(type(row) is list and len(row)==11 and all(type(x) is int and x>=0 for x in row),'trace row')
        index,reset,take,valid,sink,*payload=row
        require(index==cycle and reset==int(cycle==0) and sink==int(ready[cycle]) and take in (0,1) and valid in (0,1),'controls')
        require(valid or not any(payload),'invalid payload normalization')
        if reset:
            require(not take and not valid,'reset transfer');continue
        require(held is None or valid and tuple(payload)==held,'stalled output changed')
        if assembled and not committed and not valid:hidden+=1
        if valid:
            require(committed and tuple(payload)==committed[0],'uncommitted frame or payload order')
            if sink:committed.popleft();consumed+=1
            else:stalls+=1
        if take:
            require(accepted<len(words),'phantom acceptance')
            word=words[accepted];accepted+=1;assembled.append(word)
            if word[2]:committed.extend(assembled);assembled=[]
        held=tuple(payload) if valid and not sink else None
    require(accepted==consumed==len(words) and not assembled and not committed and held is None,'incomplete drain')
    expected=dict(accepted=accepted,consumed=consumed,flushed=0,stalls=stalls,resets=1,cycles=len(ready))
    require(all(type(metrics[k]) is int and metrics[k]==v for k,v in expected.items()),'counter mismatch')
    return dict(schemaVersion=1,valid=True,incompleteHidden=hidden,**expected)


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--configuration',required=True);parser.add_argument('--plan',required=True);parser.add_argument('--metrics',required=True)
    args=parser.parse_args()
    from fifo_reference import read_json as read
    print(json.dumps(validate(read(args.configuration),read(args.plan),read(args.metrics))))

if __name__=='__main__':main()
