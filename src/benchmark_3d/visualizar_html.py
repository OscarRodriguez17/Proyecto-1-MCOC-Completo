"""Genera un visualizador HTML interactivo 3D (Three.js) del modelo.

Fiel al modelo construido (construir.py): el anexo metalico I'-J solo existe
en los niveles superiores y el voladizo trasero del eje A3 (voladizos.py,
CONFIG_VOLADIZO) se dibuja en y<0; todo el acero en color naranja. Añade
también el arriostramiento (aspas V.M. 300x300x5, ejes G/H) y el voladizo
metalico arriostrado de piso 1 en el eje F (CONFIG_VOL_F: cordones + poste +
diagonal).

Uso:
    python src\\benchmark_3d\\visualizar_html.py

Salida: results/modelo_3d.html  (abrir en navegador, rotar y hacer zoom).
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import datos_edificio as d
from voladizos import CONFIG_VOLADIZO, CONFIG_VOL_F

OUT_DIR = os.path.join(
    os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
    "results",
)


def build(dat=d):
    xv = list(dat.GRID_X.values())
    yv = [v for _, v in sorted(dat.GRID_Y.items(), key=lambda kv: kv[1])]
    xn = list(dat.GRID_X.keys())
    yn = [k for k, _ in sorted(dat.GRID_Y.items(), key=lambda kv: kv[1])]

    anexo_lv = dat.anexo_levels()
    x_borde = dat.GRID_X[dat.ANEXO["x0"]]
    x_max = dat.GRID_X[dat.ANEXO["x1"]]
    fil_cols_y = [yv[j] for j in range(len(yn)) if yn[j] in dat.COL_EN_Y]

    col_h = []
    col_a = []
    viga_h = []
    viga_a = []
    for lvl in range(1, dat.NLEV):
        z = dat.LEVEL_Z[lvl]
        for y in yv:
            for i in range(len(xv) - 1):
                if xv[i + 1] > x_borde:
                    continue
                viga_h.append((xv[i], y, z, xv[i + 1], y, z))
        for x in xv:
            if x > x_borde:
                continue
            for j in range(len(yv) - 1):
                viga_h.append((x, yv[j], z, x, yv[j + 1], z))
        if lvl in anexo_lv:
            for y in yv:
                viga_a.append((x_borde, y, z, x_max, y, z))
            for j in range(len(yv) - 1):
                viga_a.append((x_max, yv[j], z, x_max, yv[j + 1], z))

    for st in range(dat.NLEV - 1):
        z0, z1 = dat.LEVEL_Z[st], dat.LEVEL_Z[st + 1]
        for x in xv:
            if x > x_borde:
                continue
            for y in fil_cols_y:
                col_h.append((x, y, z0, z1))
    st_pm = {st for st in range(dat.NLEV - 1)
             if st in anexo_lv and (st + 1) in anexo_lv}
    for st in sorted(st_pm):
        z0, z1 = dat.LEVEL_Z[st], dat.LEVEL_Z[st + 1]
        for y in fil_cols_y:
            col_a.append((x_max, y, z0, z1))

    cfg_v = CONFIG_VOLADIZO
    voladizo_proj = cfg_v["proj_y"]
    v_niveles = tuple(cfg_v["niveles"])
    ejes_v = [k for k in cfg_v["ejes_x"] if k in xn]
    if cfg_v.get("postes_punta", True):
        z0 = dat.LEVEL_Z[v_niveles[0]]
        z1 = dat.LEVEL_Z[v_niveles[-1]]
        for k in ejes_v:
            col_a.append((dat.GRID_X[k], 0.0 - voladizo_proj, z0, z1))
    for lvl in v_niveles:
        z = dat.LEVEL_Z[lvl]
        for k in ejes_v:
            x = dat.GRID_X[k]
            viga_h.append((x, 0.0, z, x, 0.0 - voladizo_proj, z))
        if cfg_v.get("viga_borde_punta", True) and len(ejes_v) >= 2:
            for ak, bk in zip(ejes_v[:-1], ejes_v[1:]):
                x_a, x_b = dat.GRID_X[ak], dat.GRID_X[bk]
                viga_h.append((x_a, 0.0 - voladizo_proj, z,
                               x_b, 0.0 - voladizo_proj, z))

    muros = []
    for w in dat.WALLS:
        xc, yc, L, t, resiste = dat.wall_geometry(w)
        if resiste == "Y":
            poly = [(xc - t / 2, yc - L / 2), (xc + t / 2, yc - L / 2),
                    (xc + t / 2, yc + L / 2), (xc - t / 2, yc + L / 2)]
        else:
            poly = [(xc - L / 2, yc - t / 2), (xc + L / 2, yc - t / 2),
                    (xc + L / 2, yc + t / 2), (xc - L / 2, yc + t / 2)]
        muros.append(poly)

    # aspa de arriostramiento: punta (nivel 3) -> A3 (nivel 4), ejes G/H
    aspas = []
    z_lo = dat.LEVEL_Z[v_niveles[0]]
    z_hi = dat.LEVEL_Z[v_niveles[-1]]
    for k in ejes_v:
        x = dat.GRID_X[k]
        aspas.append((x, 0.0 - voladizo_proj, z_lo, x, 0.0, z_hi))

    # voladizo metalico arriostrado de piso 1, eje F (CONFIG_VOL_F) -> RECTANGULO
    cfg_f = CONFIG_VOL_F
    if cfg_f["eje_x"] in xn:
        vf_xF = dat.GRID_X[cfg_f["eje_x"]]      # nervadura interior (eje F, x=10)
        vf_xB = cfg_f["x_borde"]                # nervadura de borde (x=17.5)
        vf_proj = cfg_f["proj_y"]
        z_inf = dat.LEVEL_Z[cfg_f["nivel_inf"]]
        z_sup = dat.LEVEL_Z[cfg_f["nivel_sup"]]
        y_tip = 0.0 - vf_proj
        for z in (z_inf, z_sup):
            # nervaduras (Y): eje F y borde, A3 -> punta
            viga_a.append((vf_xF, 0.0, z, vf_xF, y_tip, z))
            viga_a.append((vf_xB, 0.0, z, vf_xB, y_tip, z))
            # vigas de punta y de raiz (X) que cierran el rectangulo
            viga_a.append((vf_xF, y_tip, z, vf_xB, y_tip, z))
            viga_a.append((vf_xF, 0.0, z, vf_xB, 0.0, z))
        # postes metalicos en ambas puntas
        col_a.append((vf_xF, y_tip, z_inf, z_sup))
        col_a.append((vf_xB, y_tip, z_inf, z_sup))
        # diagonal en cada nervadura: punta (piso 1) -> raiz (piso 2)
        aspas.append((vf_xF, y_tip, z_inf, vf_xF, 0.0, z_sup))
        aspas.append((vf_xB, y_tip, z_inf, vf_xB, 0.0, z_sup))

    losas = []
    for lvl in range(1, dat.NLEV):
        xm = x_max if lvl in anexo_lv else x_borde
        losas.append((dat.LEVEL_Z[lvl], [(xv[0], yv[0]), (xm, yv[0]),
                                         (xm, yv[-1]), (xv[0], yv[-1])]))

    return {
        "xv": xv, "yv": yv, "xn": xn, "yn": yn,
        "col_h": col_h, "col_a": col_a,
        "viga_h": viga_h, "viga_a": viga_a,
        "muros": muros, "losas": losas,
        "aspas": aspas,
        "levels": dat.LEVEL_Z,
        "nivel_rotulo": ["Base", "Piso 1", "Piso 2", "Piso 3", "Techo"],
        "voladizo": {"proj": voladizo_proj},
    }


def main():
    g = build(d)
    os.makedirs(OUT_DIR, exist_ok=True)

    data_js = {
        "xv": g["xv"], "yv": g["yv"],
        "xn": g["xn"], "yn": g["yn"],
        "col_h": g["col_h"], "col_a": g["col_a"],
        "viga_h": g["viga_h"], "viga_a": g["viga_a"],
        "muros": g["muros"], "losas": g["losas"],
        "aspas": g["aspas"],
        "levels": g["levels"], "nivel_rotulo": g["nivel_rotulo"],
        "voladizo": g["voladizo"],
    }
    # Cartesiano -> y hacia arriba para Three.js (los niveles son Z en modelos).
    js = json.dumps(data_js)

    html = """<!DOCTYPE html>
<html lang="es">
<head>
<meta charset="utf-8">
<title>Edificio A - Modelo 3D interactivo</title>
<style>
  body{ margin:0; overflow:hidden; font-family:Segoe UI,Arial,sans-serif; }
  #info{ position:absolute; top:10px; left:50%; transform:translateX(-50%);
         background:rgba(15,20,35,.82); color:#eee; padding:8px 16px; border-radius:6px;
         font-size:13px; pointer-events:none; white-space:nowrap; }
  #leyenda{ position:absolute; left:12px; bottom:12px; background:rgba(15,20,35,.85);
            color:#eee; padding:10px 14px; border-radius:6px; font-size:12px; line-height:1.7; }
  .s{ width:18px; height:4px; margin-right:8px; display:inline-block; vertical-align:middle; }
  #gui{ position:absolute; right:12px; top:12px; color:#333; font-size:12px; }
</style>
</head>
<body>
<div id="info">Edificio de Ingeniería — Edificio A · arrastra para rotar · rueda para zoom</div>
<div id="leyenda">
  <div><span class="s" style="background:#5b6470"></span>Pilares P.70×70 (hormigón)</div>
  <div><span class="s" style="background:#3b82f6"></span>Vigas V.60/80 (hormigón)</div>
  <div><span class="s" style="background:#e65100"></span>Acero: anexo I'–J · postes + aspas + voladizo piso 1 (eje F)</div>
  <div><span class="s" style="background:#ef4444;height:10px"></span>Muros</div>
  <div><span class="s" style="background:#9ca3af"></span>Contorno losa</div>
</div>
<script src="https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js"></script>
<script>
const D = @@D@@;           // datos del modelo
const scene = new THREE.Scene();
scene.background = new THREE.Color(0xeef1f6);
const camera = new THREE.PerspectiveCamera(45, innerWidth/innerHeight, 0.1, 1000);
const renderer = new THREE.WebGLRenderer({ antialias:true });
renderer.setPixelRatio(devicePixelRatio);
renderer.setSize(innerWidth, innerHeight);
document.body.appendChild(renderer.domElement);
const controls = new THREE.OrbitControls(camera, renderer.domElement);
controls.enableDamping = true;

scene.add(new THREE.AmbientLight(0xffffff, 0.75));
const dl = new THREE.DirectionalLight(0xffffff, 0.6); dl.position.set(40,60,25); scene.add(dl);

const matCol  = new THREE.LineBasicMaterial({ color:0x5b6470 });
const matVig  = new THREE.LineBasicMaterial({ color:0x3b82f6 });
const matAce  = new THREE.LineBasicMaterial({ color:0xe65100 });
const matLosa = new THREE.LineBasicMaterial({ color:0x9ca3af });
const matMuro = new THREE.LineBasicMaterial({ color:0xef4444 });

function line(mat,x0,y0,z0,x1,y1,z1){
  const g=new THREE.BufferGeometry();
  g.setAttribute('position',new THREE.Float32BufferAttribute([x0,y0,z0,x1,y1,z1],3));
  scene.add(new THREE.Line(g,mat));
}

D.col_h.forEach(c=>line(matCol,c[0],c[1],c[2],c[0],c[1],c[3]));
D.col_a.forEach(c=>line(matAce,c[0],c[1],c[2],c[0],c[1],c[3]));
D.viga_h.forEach(v=>line(matVig,v[0],v[1],v[2],v[3],v[4],v[5]));
D.viga_a.forEach(v=>line(matAce,v[0],v[1],v[2],v[3],v[4],v[5]));
D.aspas.forEach(a=>line(matAce,a[0],a[1],a[2],a[3],a[4],a[5]));

// muros: contorno de losa trapezoidal por piso (dos caras)
D.muros.forEach(poly=>{
  for(let s=1;s<D.levels.length;s++){
    const z0=D.levels[s-1], z1=D.levels[s], pos=[];
    poly.forEach(p=>pos.push(p[0],p[1],z0)); pos.push(poly[0][0],poly[0][1],z0);
    poly.forEach(p=>pos.push(p[0],p[1],z1)); pos.push(poly[0][0],poly[0][1],z1);
    const g=new THREE.BufferGeometry();
    g.setAttribute('position',new THREE.Float32BufferAttribute(pos,3));
    scene.add(new THREE.Line(g,matMuro));
  }
});

// contorno losa por nivel
D.losas.forEach(L=>{
  const z=L[0],pts=L[1],pos=[];
  pts.forEach(p=>pos.push(p[0],p[1],z)); pos.push(pts[0][0],pts[0][1],z);
  const g=new THREE.BufferGeometry();
  g.setAttribute('position',new THREE.Float32BufferAttribute(pos,3));
  scene.add(new THREE.Line(g,matLosa));
});

// rotulos
function rotulo(txt,pos,color){
  const c=document.createElement('canvas'); c.width=c.height=256;
  const x=c.getContext('2d'); x.fillStyle=color; x.font='bold 60px Arial';
  x.textAlign='center'; x.textBaseline='middle'; x.fillText(txt,128,128);
  const s=new THREE.Sprite(new THREE.SpriteMaterial({map:new THREE.CanvasTexture(c),depthTest:false}));
  s.scale.set(1.6,1.6,1); s.position.set(pos[0],pos[1],pos[2]); scene.add(s);
}
D.xv.forEach((v,i)=>rotulo(D.xn[i],[v,-1.2,-0.6],"#2f3640"));
D.yv.forEach((v,i)=>rotulo(D.yn[i],[-1.6,v,-0.6],"#2f3640"));
D.levels.forEach((z,i)=>rotulo(D.nivel_rotulo[i]+" "+(z>=0?"+":"")+z.toFixed(2),[-3.5,-1.2,z],"#3a4a5a"));

const dx = D.xv[D.xv.length-1]-D.xv[0];
const dy = (D.yv[D.yv.length-1]-D.yv[0]) + (D.voladizo.proj);
const dz = D.levels[D.levels.length-1]-D.levels[0];
camera.position.set(dx*0.9, -dy*2.2, dz*1.9);
controls.target.set(dx*0.5, 0.1 - D.voladizo.proj*0.6, dz*0.5);
controls.update();

addEventListener('resize',()=>{ camera.aspect=innerWidth/innerHeight; camera.updateProjectionMatrix(); renderer.setSize(innerWidth,innerHeight); });
(function anim(){ requestAnimationFrame(anim); controls.update(); renderer.render(scene,camera); })();
</script>
</body>
</html>
"""
    html = html.replace("@@D@@", js)
    out = os.path.join(OUT_DIR, "modelo_3d.html")
    with open(out, "w", encoding="utf-8") as f:
        f.write(html)
    print(f"HTML interactivo guardado en: {out}")


if __name__ == "__main__":
    main()
