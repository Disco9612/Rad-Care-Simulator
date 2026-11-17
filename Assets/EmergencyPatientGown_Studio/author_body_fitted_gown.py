import bpy,math,json
from mathutils import Vector
bpy.ops.import_scene.fbx(filepath=r'D:\Rad-Care-Simulator\Assets\3D Asset\환자\source\Patient.fbx')
o=bpy.data.objects['Body'];src=o.data;verts=[];uv=[];faces=[];mapping={}
def vertex(p,tex):
 i=len(verts);verts.append({'x':-p.x/100,'y':p.y/100,'z':p.z/100});uv.append({'x':tex[0],'y':tex[1]});return i
for poly in src.polygons:
 pts=[o.matrix_world@src.vertices[i].co for i in poly.vertices];c=sum(pts,Vector())/len(pts)
 if not (-.07<c.y<.43):continue
 if c.x<-.32 or c.x>.255:continue
 if c.y>.27 and c.z>.155 and abs(c.x)<(.43-c.y)*.55:continue
 ids=[]
 for vi in poly.vertices:
  if vi not in mapping:
   v=src.vertices[vi];p=o.matrix_world@v.co;normal=o.matrix_world.to_3x3()@v.normal;normal.normalize();fold=.003*math.sin(p.x*30+p.y*5)*max(0,1-p.y);p+=normal*(.012+fold);mapping[vi]=vertex(p,(p.x*6,p.y*6))
  ids.append(mapping[vi])
 for i in range(1,len(ids)-1):faces.extend([ids[0],ids[i],ids[i+1]])
# continuous soft skirt instead of separate trouser legs
rings=[];N=80;M=22
for j in range(M+1):
 t=j/M;y=-.055-.47*t;rx=.205+.045*t;rz=.135+.008*t;row=[]
 for k in range(N):
  a=2*math.pi*k/N;wave=(.013*math.sin(9*a+3*t)+.004*math.sin(17*a-2*t))*t
  p=Vector(((rx+wave)*math.cos(a),y,.125+(rz+wave*.4)*math.sin(a)))
  row.append(vertex(p,(a/(2*math.pi)*6,t*5)))
 rings.append(row)
for j in range(M):
 for k in range(N):
  a=rings[j][k];b=rings[j][(k+1)%N];c=rings[j+1][k];d=rings[j+1][(k+1)%N];faces.extend([a,b,c,b,d,c])
# front wrap edge: sewn band near the chest
for k in range(33):
 t=k/32;p=Vector((-.11+.14*t,.42-.45*t,.255+.004*math.sin(t*math.pi)));a=vertex(p+Vector((-.004,0,0)),(0,t*4));b=vertex(p+Vector((.004,0,0)),(1,t*4))
 if k:faces.extend([a-2,a,a-1,a-1,a,b])
path=r'D:\Rad-Care-Simulator\Assets\EmergencyPatientGown_Studio\BodyFittedGown.json'
with open(path,'w',encoding='utf8') as f:json.dump({'vertices':verts,'uvs':uv,'triangles':faces},f,separators=(',',':'))
print('GOWN',len(verts),'verts',len(faces)//3,'tris',path)


