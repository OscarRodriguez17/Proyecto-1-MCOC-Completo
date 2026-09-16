import openseespy.opensees as ops
from .construir import build_model
from . import datos_edificio as D
M=build_model()
# collect model wall element midpoints and their span (from the vertical element at floor1..2)
print("MUROS del modelo (centro x,y) | sección bx x by [m] | tipo-orientación")
seen={}
for e,t in M.tipo.items():
    if t!='muro': continue
    n1,n2=ops.eleNodes(e)
    x,y,z=ops.nodeCoord(n1); key=(round(x,2),round(y,2))
    if key in seen: continue
    seen[key]=1
allw=[]
for (x,y0,y1,e) in D.MUROS_V+D.MUROS_BLOQUE_SUP_V:
    allw.append((round(x,2),round((y0+y1)/2,2),'V',round(e,2),round(y1-y0,2)))
for (y,x0,x1,e) in D.MUROS_H+D.MUROS_BLOQUE_SUP_H:
    allw.append((round((x0+x1)/2,2),round(y,2),'H',round(x1-x0,2),round(e,2)))
for (x,y,o,bx,by) in sorted(allw,key=lambda w:(-w[1],w[0])):
    inmodel = (x,y) in seen
    print(f"  ({x:6.2f},{y:6.2f})  {o}  {bx:.2f} x {by:.2f}   {'OK' if inmodel else 'FALTA'}")
print("total muros def:",len(allw)," | puntos únicos en modelo:",len(seen))