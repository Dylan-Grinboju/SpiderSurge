# Managed garbage per frame in the running game, for the state it is in now; run once per state you compare.
import argparse, json, time, urllib.error, urllib.parse, urllib.request

p = argparse.ArgumentParser()
p.add_argument("label")
p.add_argument("--owner", required=True, help="the SpiderInspector lock owner name")
p.add_argument("--seconds", type=float, default=2.0)
p.add_argument("--samples", type=int, default=4)
a = p.parse_args()
base = "http://127.0.0.1:21712"
owner = urllib.parse.quote(a.owner)

def get(path):
    sep = "&" if "?" in path else "?"
    try:
        with urllib.request.urlopen(f"{base}{path}{sep}owner={owner}", timeout=30) as r:
            return json.load(r)
    except urllib.error.HTTPError as e:
        return json.load(e)

def call(q):
    return get("/api/call?" + q)["result"]

rates = []
for _ in range(a.samples):
    call("type=System.GC&method=Collect")
    time.sleep(0.3)
    c0 = call("type=System.GC&method=CollectionCount&args=0")
    m0 = call("type=System.GC&method=GetTotalMemory&args=false")
    f0 = get("/api/time")["frame"]
    time.sleep(a.seconds)
    m1 = call("type=System.GC&method=GetTotalMemory&args=false")
    f1 = get("/api/time")["frame"]
    c1 = call("type=System.GC&method=CollectionCount&args=0")
    if c1 == c0 and f1 > f0:
        rates.append(((m1 - m0) / (f1 - f0), (f1 - f0) / a.seconds))
if rates:
    fps = sum(r[1] for r in rates) / len(rates)
    print(f"{a.label}: bytes/frame {sorted(round(r[0]) for r in rates)} fps~{fps:.0f}")
else:
    print(f"{a.label}: a collection ran in every sample; try a shorter --seconds")
