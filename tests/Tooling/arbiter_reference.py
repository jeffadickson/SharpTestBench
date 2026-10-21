"""Independent data-only arbiter policy oracle. No simulator/build orchestration."""
import argparse
import json
from fifo_reference import _pairs


def require(condition,message):
    if not condition:raise ValueError(message)


def _configuration(config):
    require(type(config) is dict and set(config)=={'Ports','RoundRobin','Blocking','LsbPriority'},'configuration fields differ')
    require(type(config['Ports']) is int and config['Ports'] in (2,3,4,8),'invalid port count')
    require(type(config['Blocking']) is int and config['Blocking'] in (0,1,2),'invalid blocking policy')
    require(type(config['RoundRobin']) is bool and type(config['LsbPriority']) is bool,'invalid policy flag')


def reference(config,plan):
    _configuration(config)
    require(type(plan) is list and 3<=len(plan)<=2100,'invalid plan length')
    ports=config['Ports'];mask=(1<<ports)-1
    order=list(range(ports))
    if not config['LsbPriority']:order.reverse()
    cursor=0;winner=None;trace=[];active=False
    for cycle,row in enumerate(plan):
        require(type(row) is dict and set(row)=={'Reset','Request','Acknowledge'},'plan row fields differ')
        require(type(row['Reset']) is bool,'invalid reset flag')
        require(all(type(row[k]) is int and 0<=row[k]<=mask for k in ['Request','Acknowledge']),'invalid request/ack mask')
        if row['Reset']:
            winner=None;cursor=0
        else:
            active |= row['Request']!=0
            retain=winner is not None and ((config['Blocking']==1 and bool(row['Request']&(1<<winner))) or
                                          (config['Blocking']==2 and not row['Acknowledge']&(1<<winner)))
            if not retain:
                candidates=(order[cursor:]+order[:cursor]) if config['RoundRobin'] else order
                winner=next((port for port in candidates if row['Request']&(1<<port)),None)
                if winner is not None and config['RoundRobin']:cursor=(order.index(winner)+1)%ports
        trace.append([cycle,int(row['Reset']),row['Request'],row['Acknowledge'],0 if winner is None else 1<<winner,
                      int(winner is not None),0 if winner is None else winner])
    require(plan[0]['Reset'] and plan[-1]['Reset'] and active,'initial/final reset and active workload required')
    return dict(trace=trace,cycles=len(trace),valid=sum(row[5] for row in trace),idle=sum(not row[5] for row in trace),
                resets=sum(row[1] for row in trace),transitions=sum(a[4]!=b[4] for a,b in zip(trace,trace[1:])))


def validate(config,plan,metrics):
    expected=reference(config,plan)
    require(type(metrics) is dict and set(metrics)==set(expected),'metric fields differ')
    for key in set(expected)-{'trace'}:
        require(type(metrics[key]) is int and metrics[key]==expected[key],'counter differs: '+key)
    trace=metrics['trace'];require(type(trace) is list and len(trace)==len(plan),'trace length differs')
    for actual,wanted in zip(trace,expected['trace']):
        require(type(actual) is list and len(actual)==7 and all(type(x) is int for x in actual),'invalid trace encoding')
        require(actual==wanted,'trace differs from requested policy or stimulus')
    require(expected['valid']>0,'empty workload')
    return dict(schemaVersion=1,valid=True,cycles=len(trace))


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for key in ['configuration','plan','metrics']:parser.add_argument('--'+key,required=True)
    args=parser.parse_args();values=[]
    for path in [args.configuration,args.plan,args.metrics]:
        with open(path,encoding='utf-8-sig') as stream:values.append(json.load(stream,object_pairs_hook=_pairs))
    print(json.dumps(validate(*values)))


if __name__=='__main__':main()
