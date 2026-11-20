"""Convert only the supplied APK's default ASE dice to a Unity JSON mesh."""
import json
import re
from pathlib import Path
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/Marble/Resources/OriginalUI'
OUT.mkdir(parents=True, exist_ok=True)
with ZipFile(r'C:\Users\ksh00\Downloads\모두의마블_10.9.02.apk') as apk:
    text = apk.read('assets/model/Dice_Default.ASE').decode('ascii')
    texture = apk.read('assets/model/texture/Dice_Default.png')
vertices = {int(i): (float(x), float(z), float(y)) for i,x,y,z in re.findall(r'\*MESH_VERTEX\s+(\d+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)', text)}
uvs = {int(i): (float(u),float(v)) for i,u,v in re.findall(r'\*MESH_TVERT\s+(\d+)\s+([-\d.]+)\s+([-\d.]+)\s+[-\d.]+', text)}
faces = {int(i): tuple(map(int,(a,b,c))) for i,a,b,c in re.findall(r'\*MESH_FACE\s+(\d+):\s+A:\s*(\d+)\s+B:\s*(\d+)\s+C:\s*(\d+)', text)}
tfaces = {int(i): tuple(map(int,(a,b,c))) for i,a,b,c in re.findall(r'\*MESH_TFACE\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)', text)}
assert len(vertices)==72 and len(faces)==140 and set(faces)==set(tfaces)
vflat,uvflat,tris=[],[],[]
for i,face in sorted(faces.items()):
    for j in (0,2,1):
        vflat.extend(x/56.133 for x in vertices[face[j]])
        uvflat.extend(uvs[tfaces[i][j]])
        tris.append(len(tris))
(OUT/'default-dice-mesh.json').write_text(json.dumps({'vertices':vflat,'uv':uvflat,'triangles':tris}),encoding='utf8')
(OUT/'default-dice-texture.png').write_bytes(texture)
print('Default dice: 72 source vertices, 140 source triangles; texture copied unchanged.')
