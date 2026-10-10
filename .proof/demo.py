import json
import os
import socket
import subprocess
import time
import urllib.error
import urllib.request

with socket.socket() as listener:
    listener.bind(('127.0.0.1',0))
    port=listener.getsockname()[1]
base=f'http://127.0.0.1:{port}'
env={**os.environ,'ASPNETCORE_URLS':base,'SUPABASE_URL':'http://127.0.0.1:1','SUPABASE_PUBLISHABLE_KEY':'proof-unreachable-upstream'}
server=subprocess.Popen(['dotnet','run','--no-build','--configuration','Release','--no-launch-profile'],env=env,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
def request(path, token=None):
    headers={'Authorization':token} if token else {}
    try:
        with urllib.request.urlopen(urllib.request.Request(base+path,headers=headers),timeout=5) as response:
            return response.status,json.loads(response.read())
    except urllib.error.HTTPError as error:
        body=error.read()
        return error.code,json.loads(body) if body else None
try:
    for attempt in range(100):
        try:
            status,health=request('/health')
            break
        except (OSError,urllib.error.URLError):
            time.sleep(.1)
    else:
        raise RuntimeError('API did not start')
    assert status==200 and health['status']=='ok'
    missing=request('/items')
    malformed=request('/items','Bearer malformed token')
    upstream=request('/items','Bearer proof-session')
    assert missing[0]==401 and malformed[0]==401 and upstream[0]==502
    print(json.dumps({'health':health,'missing_session':missing[0],'malformed_session':malformed[0],'unreachable_database':upstream[0],'database_connected':False},sort_keys=True))
finally:
    server.terminate()
    try:
        server.wait(timeout=10)
    except subprocess.TimeoutExpired:
        server.kill()
        server.wait()
