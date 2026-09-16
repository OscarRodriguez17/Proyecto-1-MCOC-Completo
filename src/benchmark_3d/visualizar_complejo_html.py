"""visualizar_complejo_html.py — HTML interactivo 3D (Three.js) del COMPLEJO.

Lee `results/edificio_completo.json` (fusionar.py) y genera
`results/modelo_complejo_3d.html` con los Edificios A y B lado a lado.

Uso:
    python src\\benchmark_3d\\visualizar_complejo_html.py

En el navegador: arrastrar para rotar, rueda para zoom.
"""

import json
import os

OUT_DIR = os.path.abspath(os.path.join(
    os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
    "results"))

JSON_COMPLEJO = os.path.join(OUT_DIR, "edificio_completo.json")

# esquema -> (categoria -> indice de color)
# 0 columnas · 1 vigas · 2 muros · 3 acero/aspas/brazos
CAT_A = {"column": 0, "wall": 2, "vigas_x": 1, "vigas_y": 1, "aspa": 3}
CAT_B = {"pilar": 0, "muro": 2, "viga": 1, "brazo": 3}


def _segmentos_three(edificio):
    """Devuelve ([x0,y0,z0,x1,y1,z1,ci, ...], (cx, cy, zmin)) en coords THREE.
    THREE: X=x, Y=z(nivel, arriba), Z=y (profundidad)."""
    js = edificio["json"]
    segs = []
    xs, ys, zs = [], [], []
    if edificio["esquema"] == "A":
        nodos = {int(k): v for k, v in js["nodos"].items()}
        for el in js["elementos"]:
            ni, nj = nodos[el["ni"]], nodos[el["nj"]]
            p = (ni["x"], ni["y"], ni["z"], nj["x"], nj["y"], nj["z"],
                 CAT_A.get(el["tipo"], 1))
            segs.append(p)
            xs += [ni["x"], nj["x"]]; ys += [ni["y"], nj["y"]]
            zs += [ni["z"], nj["z"]]
    else:
        nodos = {n["id"]: n for n in js["nodos"]}
        for el in js["elementos"]:
            ni, nj = nodos[el["ni"]], nodos[el["nj"]]
            segs.append((ni["x"], ni["y"], ni["z"], nj["x"], nj["y"], nj["z"],
                         CAT_B.get(el["tipo"], 1)))
            xs += [ni["x"], nj["x"]]; ys += [ni["y"], nj["y"]]
            zs += [ni["z"], nj["z"]]
    cx = (min(xs) + max(xs)) / 2.0
    label = [cx, min(zs), (min(ys) + max(ys)) / 2.0]      # (x, z?, y)  <- THREE x, y, z
    return segs, label


def main():
    with open(JSON_COMPLEJO, encoding="utf-8") as f:
        data = json.load(f)

    segs = []
    labels = []
    for ed in data["edificios"]:
        s, lbl = _segmentos_three(ed)
        segs.append({"id": ed["id"], "nombre": ed["nombre"], "segs": s,
                     "label": lbl})

    all_pts = [p for ed_block in segs for p in ed_block["segs"]]
    xs = [p[0] for p in all_pts] + [p[3] for p in all_pts]
    zs = [p[2] for p in all_pts] + [p[5] for p in all_pts]
    dy = _span_y(segs)
    x_min, x_max = min(xs), max(xs)
    z_min, z_max = min(zs), max(zs)
    dx = x_max - x_min
    dz = z_max - z_min
    span_y = max(z_min * 0, dy) + 1
    _ = span_y

    js = json.dumps(segs)
    cen = json.dumps([(x_min + x_max) / 2.0, 0.0, (z_min + z_max) / 2.0])
    cam = json.dumps([dx * 0.9, dz * 0.8, -dy * 1.6])

    html = """<!DOCTYPE html>
<html lang="es">
<head>
<meta charset="utf-8">
<title>Complejo de Ingeniería — Edificios A y B</title>
<style>
  body{ margin:0; overflow:hidden; font-family:Segoe UI,Arial,sans-serif; }
  #info{ position:absolute; top:10px; left:50%; transform:translateX(-50%);
         background:rgba(15,20,35,.82); color:#eee; padding:8px 16px; border-radius:6px;
         font-size:13px; pointer-events:none; white-space:nowrap; }
  #leyenda{ position:absolute; left:12px; bottom:12px; background:rgba(15,20,35,.85);
            color:#eee; padding:10px 14px; border-radius:6px; font-size:12px; line-height:1.7; }
  .s{ width:18px; height:4px; margin-right:8px; display:inline-block; vertical-align:middle; }
</style>
</head>
<body>
<div id="info">COMPLEJO — Edificios A y B · arrastra para rotar · rueda para zoom</div>
<div id="leyenda">
  <div><span class="s" style="background:#5b6470"></span>Columnas / pilares</div>
  <div><span class="s" style="background:#3b82f6"></span>Vigas (hormigón)</div>
  <div><span class="s" style="background:#ef4444;height:10px"></span>Muros</div>
  <div><span class="s" style="background:#e65100"></span>Acero / aspas / brazos rígidos</div>
</div>
<script src="https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js"></script>
<script>
const BLOCKS = @@BLOCKS@@;        // [{id, nombre, segs:[x0,y0,z0,x1,y1,z1,ci], label:[x,y,z]}]
const CENTRO = @@CENTRO@@;
const CAM    = @@CAM@@;
const scene = new THREE.Scene();
scene.background = new THREE.Color(0xeef1f6);
const camera = new THREE.PerspectiveCamera(45, innerWidth/innerHeight, 0.1, 2000);
camera.position.set(CAM[0], CAM[2], CAM[1]);
const renderer = new THREE.WebGLRenderer({ antialias:true });
renderer.setPixelRatio(devicePixelRatio);
renderer.setSize(innerWidth, innerHeight);
document.body.appendChild(renderer.domElement);
const controls = new THREE.OrbitControls(camera, renderer.domElement);
controls.target.set(CENTRO[0], CENTRO[2], CENTRO[1]);
controls.enableDamping = true;

scene.add(new THREE.AmbientLight(0xffffff, 0.75));
const dl = new THREE.DirectionalLight(0xffffff, 0.6); dl.position.set(60,80,30); scene.add(dl);

const matCol = ['#5b6470','#3b82f6','#ef4444','#e65100'];
const mats = matCol.map(c=>new THREE.LineBasicMaterial({color:new THREE.Color(c)}));
const matGround = new THREE.LineBasicMaterial({color:0x9ca3af});

function line(mat,x0,y0,z0,x1,y1,z1){
  const g=new THREE.BufferGeometry();
  g.setAttribute('position',new THREE.Float32BufferAttribute([x0,y0,z0,x1,y1,z1],3));
  scene.add(new THREE.Line(g,mat));
}

BLOCKS.forEach(b=>{
  b.segs.forEach(s=>line(mats[s[6]],s[0],s[1],s[2],s[3],s[4],s[5]));
  // etiqueta "EDIFICIO X" a nivel del suelo (matelas: y = z del label = nivel base)
  const c=document.createElement('canvas'); c.width=1024; c.height=128;
  const x=c.getContext('2d'); x.fillStyle='rgba(15,20,35,.15)'; x.fillRect(0,0,1024,128);
  x.fillStyle='#16283a'; x.font='bold 84px Arial'; x.textAlign='center'; x.textBaseline='middle';
  x.fillText('EDIFICIO '+b.id,512,64);
  const spr=new THREE.Sprite(new THREE.SpriteMaterial({map:new THREE.CanvasTexture(c),depthTest:false}));
  spr.scale.set(14,1.8,1); spr.position.set(b.label[0], b.label[1]-0.8, b.label[2]);
  scene.add(spr);
});

// linea de terreno a lo ancho del complejo
line(matGround, CENTRO[0]-60, CAM[1]*0.05, CENTRO[2], CENTRO[0]+60, CAM[1]*0.05, CENTRO[2]);

addEventListener('resize',()=>{ camera.aspect=innerWidth/innerHeight; camera.updateProjectionMatrix(); renderer.setSize(innerWidth,innerHeight); });
(function anim(){ requestAnimationFrame(anim); controls.update(); renderer.render(scene,camera); })();
</script>
</body>
</html>
"""
    html = html.replace("@@BLOCKS@@", js)
    html = html.replace("@@CENTRO@@", cen)
    html = html.replace("@@CAM@@", cam)

    out = os.path.join(OUT_DIR, "modelo_complejo_3d.html")
    with open(out, "w", encoding="utf-8") as f:
        f.write(html)
    print(f"HTML complejo -> {out}")


def _span_y(blocks):
    ys = []
    for b in blocks:
        for s in b["segs"]:
            ys += [s[1], s[4]]
    return (max(ys) - min(ys)) + 8 if ys else 60.0


if __name__ == "__main__":
    main()