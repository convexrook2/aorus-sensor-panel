"""Build original, reference-guided Expanse fan models, using the Razorback path.

Build-time only: numpy, Pillow, trimesh; Pella also uses PyMeshLab. +Y is forward, +Z the presentation face.
Pella adapts Martin Taichl's credited CC BY 4.0 mesh; other geometry, panel
textures and decals are authored here. Reference images are never read as
textures or converted into geometry. Visible proportions are an
approximation; unseen fittings are interpreted, not official production detail.
Run with the bundled Python; trimesh can live in artifacts/ship-model-build-deps.
"""
from __future__ import annotations
import argparse
from collections import defaultdict
import hashlib
import io
import json
import math
from pathlib import Path
import sys

sys.dont_write_bytecode = True

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'artifacts/ship-model-build-deps'))
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import trimesh
from trimesh.visual.material import PBRMaterial
from trimesh.visual.texture import TextureVisuals
import importlib.util
_spec = importlib.util.spec_from_file_location('fleet_converter', ROOT / 'tools/Convert-HoloScene.py')
_converter = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_converter)


def linear(rgb):
    c = np.asarray(rgb, float) / 255
    return np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)


def font(size):
    return ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf', size)


class Hull:
    def __init__(self, name, paint=(100, 112, 114), accent=(180, 76, 37)):
        self.name, self.parts = name, defaultdict(list)
        self.paint, self.accent = paint, accent
        self.rng = np.random.default_rng(sum(map(ord, name)) + 32617)
        # A single authored atlas per model, with a dedicated stencil column.
        noise = self.rng.normal(0, 1.8, (1024, 1024, 1))
        self.atlas = Image.fromarray(np.uint8(np.clip(np.array(paint) + noise, 0, 255)))
        d = ImageDraw.Draw(self.atlas)
        dark = tuple(max(0, x - 29) for x in paint)
        edge = tuple(min(255, x + 18) for x in paint)
        for x in range(0, 769, 192):
            d.line((x, 0, x, 1024), fill=dark, width=3)
            d.line((x+3, 0, x+3, 1024), fill=edge, width=1)
        for y in range(0, 1025, 128):
            d.line((0, y, 768, y), fill=dark, width=3)
            for x in range(12, 768, 192):
                d.ellipse((x, y+9, x+4, y+13), fill=dark)
                d.ellipse((x+161, y+9, x+165, y+13), fill=dark)
        for j in range(80):
            x, y = self.rng.integers(6, 760), self.rng.integers(6, 1010)
            d.line((x, y, min(765, x+self.rng.integers(2, 14)), y+1), fill=edge, width=1)
        labels = [name.upper(), 'SERVICE / 04', 'AIRLOCK', 'KEEP CLEAR', 'CAUTION', 'RCS / 02', 'ENGINEERING', 'FAN MADE']
        for j, text in enumerate(labels):
            y = j * 128
            d.rectangle((768, y, 1024, y+128), fill=paint)
            d.line((782, y+15, 1009, y+15), fill=accent, width=4)
            fs = min(38, int(390 / max(5, len(text)) * 1.2))
            f = font(fs); bb = d.textbbox((0, 0), text, font=f)
            d.text((896-(bb[2]-bb[0])/2, y+47), text, font=f, fill=(227, 229, 218))
            d.line((782, y+100, 1009, y+100), fill=dark, width=2)
        def material(key, rgb, metal=.35, rough=.62, glow=None, atlas=False):
            color = [*linear(rgb), 1]
            kw = dict(name=key, baseColorFactor=np.array(color), metallicFactor=metal, roughnessFactor=rough)
            if atlas:
                kw['baseColorFactor'] = np.ones(4)
                kw['baseColorTexture'] = self.atlas
            if glow is not None:
                kw['emissiveFactor'] = np.asarray(glow, float)
            return PBRMaterial(**kw)
        self.materials = {
            'paint': material('Authored hull panels and stencils', paint, atlas=True),
            'frame': material('Exposed structural alloy', (76, 84, 88), .72),
            'dark': material('Recessed machinery', (28, 34, 39), .6),
            'black': material('Stealth ceramic', (30, 35, 42), .12, .82),
            'silver': material('Machined titanium', (143, 151, 153), .82, .38),
            'white': material('Pale ceramic panels', (198, 201, 193), .15),
            'accent': material('Ship paint accents', accent, .2),
            'copper': material('Thermal plumbing', (131, 90, 50), .72),
            'glass': material('Dark bridge glazing', (21, 48, 58), .55, .2),
            'blue': material('Drive throat', (58, 151, 219), .2, .5, (.09, .32, .65)),
            'lamp': material('Warm work lights', (232, 192, 116), .0, .5, (.65, .40, .12)),
            'nav': material('Navigation lights', (91, 190, 206), .0, .5, (.12, .48, .55)),
        }

    def add(self, mesh, key='paint'):
        if len(mesh.faces) == 0:
            return
        # Flat structural edges; round cylinders retain their authored normals.
        if key == 'paint':
            mesh = mesh.copy()
            mesh.unmerge_vertices()
            p = mesh.vertices
            n = np.repeat(mesh.face_normals, 3, axis=0)
            ax = np.argmax(np.abs(n), axis=1)
            u = np.where(ax == 0, p[:, 2], p[:, 0])
            v = np.where(ax == 1, p[:, 2], p[:, 1])
            # Keep every vertex inside the panel region, without wrapping an
            # individual triangle across the atlas's stencil column.
            uv = np.column_stack((np.clip(.5+u/4,.01,.99)*.73+.008, np.clip(.5+v/5,.01,.99)))
        else:
            uv = np.zeros((len(mesh.vertices), 2))
        mesh.visual = TextureVisuals(uv=uv, material=self.materials[key])
        self.parts[key].append(mesh)
        return mesh

    def box(self, p, size, key='paint', rotation=None):
        # Six main faces, twelve bevels and eight clipped corners. Even small
        # native portraits retain an edge highlight instead of a flat rectangle.
        extent=np.array(size,float)/2
        b=min(float(min(extent))*.28,.025)
        vertices=[]; faces=[]
        def polygon(points):
            start=len(vertices);points=np.array(points,float)
            normal=np.cross(points[1]-points[0],points[2]-points[0])
            if np.dot(normal,points.mean(axis=0))<0:points=points[::-1]
            vertices.extend(points)
            for i in range(1,len(points)-1):faces.append([start,start+i,start+i+1])
        for axis in range(3):
            other=[k for k in range(3) if k!=axis]
            for sign in [-1,1]:
                points=[]
                for a,c in [(-1,-1),(1,-1),(1,1),(-1,1)]:
                    v=np.zeros(3);v[axis]=sign*extent[axis]
                    v[other[0]]=a*(extent[other[0]]-b);v[other[1]]=c*(extent[other[1]]-b)
                    points.append(v)
                polygon(points)
        for axis in range(3):
            other=[k for k in range(3) if k!=axis]
            for s in [-1,1]:
                for t in [-1,1]:
                    points=[]
                    for side,z in [(0,-1),(1,-1),(1,1),(0,1)]:
                        v=np.zeros(3);v[axis]=z*(extent[axis]-b)
                        v[other[0]]=s*(extent[other[0]]-(b if side else 0))
                        v[other[1]]=t*(extent[other[1]]-(0 if side else b))
                        points.append(v)
                    polygon(points)
        for sx in [-1,1]:
            for sy in [-1,1]:
                for sz in [-1,1]:
                    s=np.array([sx,sy,sz]);points=[]
                    for axis in range(3):
                        v=s*(extent-b);v[axis]=s[axis]*extent[axis];points.append(v)
                    polygon(points)
        m=trimesh.Trimesh(vertices=vertices,faces=faces,process=False)
        if rotation is not None:
            m.apply_transform(rotation)
        m.apply_translation(p)
        return self.add(m, key)

    def beam(self, a, b, r=.012, key='frame', sections=8):
        a, b = np.array(a, float), np.array(b, float)
        v = b-a; length = np.linalg.norm(v)
        if length < 1e-8:
            return
        m = trimesh.creation.cylinder(radius=r, height=length, sections=sections)
        m.apply_transform(trimesh.geometry.align_vectors([0, 0, 1], v/length))
        m.apply_translation((a+b)/2)
        return self.add(m, key)

    def sphere(self, p, r, key='silver', scale=(1, 1, 1)):
        m = trimesh.creation.icosphere(subdivisions=2, radius=r)
        m.apply_scale(scale); m.apply_translation(p)
        return self.add(m, key)

    def lathe(self, profile, p=(0, 0, 0), key='silver', sides=40, closed=True):
        """(Y,radius) meridian, including inner wall when a tube is required."""
        points, faces = [], []
        for y, r in profile:
            points.extend([[p[0]+r*math.cos(t), p[1]+y, p[2]+r*math.sin(t)] for t in np.arange(sides)*2*math.pi/sides])
        for j in range(len(profile)-1):
            for i in range(sides):
                a=j*sides+i; b=j*sides+(i+1)%sides; c=b+sides; e=a+sides
                faces.extend([[a,b,c],[a,c,e]])
        if closed:
            for i in range(1,sides-1):
                faces.extend([[0,i+1,i],[(len(profile)-1)*sides,(len(profile)-1)*sides+i,(len(profile)-1)*sides+i+1]])
        m = trimesh.Trimesh(vertices=points, faces=faces, process=False)
        # Correct consistent winding for meridians built in either direction.
        if m.volume < 0: m.invert()
        return self.add(m, key)

    def ring(self, y, radius, width=.025, depth=.035, p=(0,0,0), key='silver', sides=40):
        return self.lathe([(y-depth/2,radius-width/2),(y-depth/2,radius+width/2),
                           (y+depth/2,radius+width/2),(y+depth/2,radius-width/2),
                           (y-depth/2,radius-width/2)], p, key, sides, False)

    def loft(self, stations, section=None, key='paint', cap=True):
        # Station = (Y, halfwidth, halfdepth[, centerX, centerZ]).
        if section is None:
            section=[(-.72,-1),(.72,-1),(1,-.72),(1,.72),(.72,1),(-.72,1),(-1,.72),(-1,-.72)]
        n=len(section); verts=[]; faces=[]
        for s in stations:
            y,w,d=s[:3]; cx,cz=s[3:] if len(s)>3 else (0,0)
            verts.extend([[cx+x*w,y,cz+z*d] for x,z in section])
        for j in range(len(stations)-1):
            for i in range(n):
                a=j*n+i; b=j*n+(i+1)%n; c=b+n; e=a+n
                faces.extend([[a,b,c],[a,c,e]])
        if cap:
            for i in range(1,n-1):
                faces.extend([[0,i+1,i],[(len(stations)-1)*n,(len(stations)-1)*n+i,(len(stations)-1)*n+i+1]])
        m=trimesh.Trimesh(vertices=verts,faces=faces,process=False)
        if m.volume<0:m.invert()
        return self.add(m,key)

    def panel(self, p, w, h, key='paint', rotation=None, ribs=0):
        self.box(p,(w,h,.018),key,rotation)
        if rotation is None:
            for i in range(ribs):
                self.box((p[0]-w/2+(i+.5)*w/ribs,p[1],p[2]+.012),(.007,h*.9,.009),'silver')

    def decal(self, p, w=.24, h=.11, label=0, rotation=None):
        v=np.array([[-w/2,-h/2,0],[w/2,-h/2,0],[w/2,h/2,0],[-w/2,h/2,0]])
        if rotation is not None:
            v=trimesh.transform_points(v,rotation)
        v+=p
        m=trimesh.Trimesh(vertices=v,faces=[[0,1,2],[0,2,3]],process=False)
        top=1-label/8; bottom=top-1/8
        m.visual=TextureVisuals(uv=np.array([[.75,bottom],[1,bottom],[1,top],[.75,top]]),material=self.materials['paint'])
        self.parts['paint'].append(m)

    def engine(self, p, r=.2, depth=.38, collars=5):
        # Open bell with inner lining and a recessed, emissive throat; no plume.
        self.lathe([(0,r*.50),(-depth*.2,r*.57),(-depth*.55,r*.77),(-depth,r),
                    (-depth,r*.89),(-depth*.55,r*.68),(-depth*.2,r*.43)],p,'frame',48)
        for t in np.linspace(.22,.92,collars):
            self.ring(-depth*t,r*(.42+.58*t),r*.055,r*.065,p,'silver',40)
        self.lathe([(-depth*.16,0),(-depth*.17,r*.40)],p,'blue',32,False)
        for a in np.arange(8)*math.pi/4:
            x,z=p[0]+r*.50*math.cos(a),p[2]+r*.50*math.sin(a)
            self.beam((x,p[1]+.07,z),(p[0]+r*.87*math.cos(a),p[1]-depth*.82,p[2]+r*.87*math.sin(a)),r*.026,'copper')

    def rcs(self,p,axis=(0,0,1),r=.025):
        p=np.array(p); axis=np.array(axis,float)
        self.beam(p,p+axis*.065,r,'silver',12)
        self.beam(p+axis*.06,p+axis*.071,r*.73,'dark',12)

    def pdc(self,p,scale=.1,axis=(0,1,0)):
        p=np.array(p); axis=np.array(axis,float); axis/=np.linalg.norm(axis)
        self.sphere(p,scale*.58,'dark',(1,.85,1))
        self.box(p+axis*scale*.35,(scale*.8,scale*.7,scale*.8),'paint')
        side=np.cross(axis,[0,0,1])
        if np.linalg.norm(side)<.1:side=np.array([1,0,0])
        side/=np.linalg.norm(side); up=np.cross(axis,side)
        for a in np.arange(6)*math.pi/3:
            start=p+axis*scale*.4+(side*math.cos(a)+up*math.sin(a))*scale*.20
            self.beam(start,start+axis*scale*1.35,scale*.065,'silver',8)

    def ladder(self,p,steps=9,height=.5):
        x,y,z=p
        for dx in [-.035,.035]:self.beam((x+dx,y-height/2,z),(x+dx,y+height/2,z),.004,'silver',6)
        for yy in np.linspace(y-height/2,y+height/2,steps):self.beam((x-.035,yy,z),(x+.035,yy,z),.0035,'silver',6)

    def export(self,folder):
        scene=trimesh.Scene()
        for key,items in self.parts.items():
            # Concatenate by material, retaining individually authored UV islands.
            vertices=[];faces=[];uv=[];authored_normals=[];offset=0
            for m in items:
                vertices.append(m.vertices);faces.append(m.faces+offset);offset+=len(m.vertices)
                uv.append(m.visual.uv)
                if getattr(self, 'preserve_authored_normals', False):
                    normals=np.asarray(m.vertex_normals)
                    if normals.shape != m.vertices.shape or not np.isfinite(normals).all() or not np.allclose(np.linalg.norm(normals,axis=1),1,atol=1e-4):
                        raise ValueError(self.name+' has invalid authored normals')
                    authored_normals.append(normals)
            m=trimesh.Trimesh(vertices=np.vstack(vertices),faces=np.vstack(faces),process=False)
            # Area-weighted normals using NumPy: avoids an optional SciPy
            # dependency and keeps this build path self-contained.
            triangles=m.vertices[m.faces]
            area=np.cross(triangles[:,1]-triangles[:,0],triangles[:,2]-triangles[:,0])
            normals=np.zeros_like(m.vertices)
            for corner in range(3):np.add.at(normals,m.faces[:,corner],area)
            lengths=np.linalg.norm(normals,axis=1)
            m.vertex_normals=np.vstack(authored_normals) if authored_normals else normals/np.maximum(lengths[:,None],1e-15)
            m.visual=TextureVisuals(uv=np.vstack(uv),material=self.materials[key])
            scene.add_geometry(m,node_name=key,geom_name=key)
        if scene.triangles.shape[0]>150000:raise ValueError(self.name+' exceeds native budget')
        folder.mkdir(parents=True,exist_ok=True)
        path=folder/(self.name+'.glb')
        raw=trimesh.exchange.gltf.export_glb(scene,include_normals=True)
        path.write_bytes(raw)
        return path, {'id':self.name,'triangles':int(scene.triangles.shape[0]),'materials':len(self.parts),
                      'components':sum(len(p) for p in self.parts.values()),'bounds':scene.bounds.tolist(),
                      'sourceSha256':hashlib.sha256(raw).hexdigest()}


def rot(axis,angle):
    return trimesh.transformations.rotation_matrix(math.radians(angle),axis)


def polar(radius,angle,y=0):
    a=math.radians(angle);return (radius*math.cos(a),y,radius*math.sin(a))


def tynan():
    h=Hull('Tynan',(105,112,100),(199,119,47))
    # Frank Yang's five orthographic views: open industrial frame, tall armour
    # cheeks, offset cargo equipment, and a single exposed oversized bell.
    h.loft([(-1.05,.32,.31),(-.65,.35,.35),(.65,.34,.32),(1.12,.25,.25)],key='dark')
    for a in [30,90,150,210,270,330]:
        x,z=polar(.51,a)[::2]
        h.beam((x,-1.13,z),(x,1.27,z),.027,'frame')
        for yy in [-1.06,-.62,-.12,.43,.93,1.25]:
            nx,nz=polar(.51,a+60)[::2]
            h.beam((x,yy,z),(nx,yy,nz),.022,'frame')
        for yy in [-.85,-.3,.25,.8]:
            nx,nz=polar(.51,a+60)[::2]
            h.beam((x,yy-.25,z),(nx,yy+.25,nz),.009,'copper')
    # Cheeks leave deep openings around the working central spine.
    for side in [-1,1]:
        R=rot((0,1,0),side*15)
        h.loft([(-.04,.085,.23,side*.45,.08),(.24,.145,.34,side*.49,.08),
                (1.15,.145,.34,side*.49,.08),(1.33,.10,.24,side*.45,.08)])
        h.loft([(-.90,.09,.24,side*.45,.03),(-.77,.145,.34,side*.49,.03),
                (-.39,.145,.34,side*.49,.03),(-.25,.10,.26,side*.45,.03)])
        h.box((side*.55,-.03,-.36),(.21,.62,.24),'white')
        for yy in [.29,.49,.69,.89,1.09]:
            h.box((side*.535,yy,.438),(.18,.013,.023),'dark')
        h.box((side*.43,1.35,-.17),(.14,.23,.37),'frame',rot((0,0,1),side*-23))
        # Separate mounting pads, exposed hoses and access panels follow the
        # divisions visible between the concept's larger cheek plates.
        for yy in [.39,.73,1.02]:
            h.box((side*.49,yy,.444),(.18,.18,.022),'paint')
            for dx in [-.06,.06]:h.sphere((side*.49+dx,yy+.06,.460),.008,'silver')
        h.beam((side*.24,-.65,.35),(side*.24,.94,.35),.012,'copper')
        h.beam((side*.30,-.52,.30),(side*.30,.75,.30),.018,'silver')
        h.beam((side*.48,1.42,-.27),(side*.57,1.78,-.22),.009,'silver')
        h.beam((side*.52,.20,-.35),(side*.95,.75,-.64),.009,'copper')
        h.beam((side*.49,-.50,-.38),(side*.82,-1.2,-.58),.012,'silver')
        h.sphere((side*.34,-1.13,.18),.13,'silver')
        h.lathe([(-.11,.11),(.11,.11)],(side*.46,.13,-.37),'dark',16)
    h.box((.02,.55,.36),(.30,1.18,.21),'paint')
    h.box((.02,-.48,.39),(.31,.46,.19),'accent')
    h.box((.02,-.48,.495),(.22,.29,.026),'dark')
    h.decal((.02,-.48,.51),.22,.28,0)
    h.box((.05,.03,.45),(.24,.27,.08),'silver')
    h.decal((.05,.03,.495),.21,.13,2)
    h.ladder((-.19,.53,.49),12,.96)
    for y in [-.88,-.2,.55,1.06]:
        h.box((-.27,y,.33),(.08,.12,.08),'dark')
        h.box((-.27,y,.379),(.055,.012,.018),'lamp')
    h.lathe([(-.20,.32),(.0,.43),(.18,.35)],(0,-1.22,0),'copper',32)
    h.engine((0,-1.39,0),.43,.53)
    for side in [-1,1]:
        h.beam((side*.29,-1.03,.28),(side*.46,-1.51,.37),.019,'silver')
        h.rcs((side*.60,.92,.12),(side,0,0))
        h.rcs((side*.56,-.68,-.06),(side,0,0))
    h.box((0,1.32,-.10),(.19,.20,.26),'dark')
    h.beam((0,1.35,-.13),(0,1.99,-.13),.006,'silver')
    h.beam((.18,1.30,-.24),(.18,1.72,-.24),.005,'silver')
    return h


def morrigan():
    h=Hull('Morrigan',(117,126,125),(174,66,34))
    # Triangular end section, traced width changes and single drive from the
    # existing Mallacore sheet, rather than the Rocinante's octagonal hull.
    tri=[(0,1),(-.84,-.42),(-.72,-.69),(.72,-.69),(.84,-.42)]
    h.loft([(-1.25,.43,.44),(-.93,.48,.46),(.56,.47,.43),(1.15,.41,.39),(1.48,.25,.31)],tri)
    h.engine((0,-1.26,-.05),.36,.36)
    for y in [-.99,-.67,-.30,.08,.49,.87,1.15]:
        h.box((0,y,.435 if y<.5 else .405),(.39,.012,.015),'dark')
    for s in [-1,1]:
        R=rot((0,1,0),s*90)
        h.panel((s*.407,.18,-.04),.40,1.35,'paint',R)
        h.box((s*.40,1.12,-.10),(.10,.31,.23),'accent')
        h.box((s*.34,.88,-.27),(.14,.30,.022),'dark')
        for yy in [-.85,-.72,-.59,-.46]:
            h.box((s*.26,yy,.40),(.13,.047,.022),'dark')
        h.rcs((s*.39,-.96,.08),(s,0,0),.021)
        h.rcs((s*.31,1.27,.02),(s,0,0),.018)
        h.box((s*.20,1.33,.243),(.15,.13,.018),'glass')
        h.beam((s*.34,-.9,-.29),(s*.39,-1.3,-.37),.008,'silver')
    h.box((0,.12,.45),(.27,.28,.026),'dark')
    h.decal((0,.13,.466),.23,.12,0)
    h.decal((0,.78,.420),.25,.11,4)
    h.pdc((.32,-.25,.34),.08,(0,1,.12))
    h.pdc((-.32,.74,-.25),.08,(0,1,-.2))
    for yy in [-.78,-.42,.0,.40]:
        for s in [-1,1]:
            h.panel((s*.145,yy,.453),.12,.17,'paint')
            h.box((s*.145,yy-.060,.468),(.078,.007,.006),'silver')
    for s in [-1,1]:
        h.beam((s*.30,-1.08,.32),(s*.30,.64,.32),.009,'silver')
    h.beam((.16,1.4,.15),(.16,1.74,.15),.004,'silver')
    return h


def canterbury():
    h=Hull('Canterbury',(109,115,109),(170,137,62))
    # Six-sided open ice bay. The space between rails is deliberately empty.
    radius=.67; angles=[30,90,150,210,270,330]
    for y in [-.92,-.55,-.12,.31,.74,1.17]:
        h.ring(y,radius,.055,.055,key='frame',sides=6)
    for i,a in enumerate(angles):
        x,z=polar(radius,a)[::2]
        h.beam((x,-1.06,z),(x,1.24,z),.04,'frame')
        xx,zz=polar(radius,a+60)[::2]
        for y in [-.75,-.32,.11,.54,.97]:
            h.beam((x,y-.18,z),(xx,y+.18,zz),.012,'silver')
        # Cargo rack frames remain behind the open intake.
        for y in [-.58,.03,.64]:
            xx,zz=polar(radius*.82,a)[::2]
            h.box((xx,y,zz),(.16,.36,.18),'dark')
            h.box((xx,y+.12,zz+.10),(.12,.027,.025),'lamp')
    h.loft([(-1.36,.62,.59),(-1.10,.72,.64),(-.96,.64,.61)],key='paint')
    # Habitation and service modules run along the dorsal side of the bay.
    h.box((0,.07,.72),(.65,1.86,.29),'paint')
    h.box((0,.12,.91),(.37,.80,.13),'dark')
    h.box((0,.55,.99),(.38,.16,.07),'glass')
    h.decal((0,.12,.986),.34,.17,0)
    for s in [-1,1]:
        h.box((s*.51,-.10,-.56),(.25,1.56,.27),'paint')
        for yy in [-.68,-.40,-.12,.16,.44]:
            h.box((s*.51,yy,-.704),(.19,.018,.018),'accent')
        h.beam((s*.32,.90,.77),(s*.60,1.46,.89),.014,'frame')
        h.rcs((s*.58,1.18,.36),(s,0,0),.030)
        # Intake manipulator with a visible elbow and fork.
        h.beam((s*.53,.56,-.12),(s*.29,.30,-.10),.023,'silver')
        h.sphere((s*.29,.30,-.10),.05,'dark')
        h.beam((s*.29,.30,-.10),(s*.20,.63,-.04),.019,'copper')
        for dz in [-.055,.055]:h.beam((s*.20,.63,-.04),(s*.09,.74,-.04+dz),.012,'silver')
    for a in [45,135,225,315]:
        x,z=polar(.53,a)[::2]
        for dx in [-.105,.105]:h.engine((x+dx,-1.40,z),.155,.30,4)
    h.ring(1.22,.68,.048,.048,key='silver',sides=6)
    # A few irregular ice masses leave the mechanism and empty bay readable.
    for p,r in [((.12,-.32,-.17),.20),((-.14,.10,.05),.21),((.10,.56,-.12),.17)]:
        h.sphere(p,r,'white',(.9,1.25,.8))
    return h


def knight():
    h=Hull('Knight',(143,148,140),(165,132,62))
    h.loft([(-1.03,.43,.36),(-.66,.49,.42),(.53,.45,.37),(.99,.31,.30),(1.20,.20,.21)])
    for s in [-1,1]:
        h.box((s*.28,.89,.25),(.20,.31,.018),'glass',rot((0,1,0),s*18))
        h.box((s*.43,-.37,-.05),(.20,.61,.44),'paint')
        h.engine((s*.30,-1.03,-.04),.17,.27,4)
        h.beam((s*.30,-.51,-.30),(s*.62,-.77,-.48),.022,'silver')
        h.box((s*.62,-.77,-.49),(.23,.16,.06),'dark')
        h.rcs((s*.42,.50,.03),(s,0,0),.024)
    h.box((0,-.35,.427),(.40,.53,.05),'dark')
    h.decal((0,-.35,.454),.35,.18,2)
    h.decal((0,.27,.399),.36,.14,0)
    h.ladder((.30,-.3,.43),8,.5)
    return h


def anubis():
    h=Hull('Anubis',(38,44,53),(92,106,112))
    # Deliberately asymmetric faceted stealth envelope, without exposed
    # industrial greebles on its radar-facing surfaces.
    section=[(-1,-.33),(-.38,-1),(.71,-.78),(1,.12),(.28,1),(-.81,.73)]
    h.loft([(-1.19,.52,.41,-.06,0),(-.60,.67,.47,-.03,.01),
            (.15,.62,.45,.02,0),(.93,.43,.32,.13,-.04),(1.45,.14,.16,.22,-.09)],section)
    h.engine((-.06,-1.18,0),.34,.40,4)
    for s in [-1,1]:
        h.box((s*.38,.28,.345),(.24,.52,.016),'black',rot((0,1,0),s*18))
        h.box((s*.36,-.54,.403),(.24,.30,.014),'black')
        h.box((s*.36,-.54,.414),(.19,.008,.008),'frame')
    h.loft([(.48,.14,.12,.25,-.21),(1.51,.08,.065,.28,-.18),(1.66,.05,.045,.28,-.18)],key='black')
    h.beam((.28,1.59,-.18),(.28,1.78,-.18),.023,'dark',8)
    h.box((-.20,.94,.225),(.12,.16,.017),'glass')
    h.box((.14,-.72,.457),(.04,.010,.010),'nav')
    h.decal((-.03,-.25,.480),.18,.075,0)
    return h


def pella():
    from PellaModel import build_pella
    return build_pella(Hull)


def bashi_bazouk():
    from BashiBazoukModel import build_bashi
    return build_bashi(Hull)


def nauvoo(name='Nauvoo'):
    accent=(168,143,79) if name=='Nauvoo' else (184,102,49)
    h=Hull(name,(166,170,163),accent)
    # Shared generation-ship drum and eight-drive array; each era below has
    # separate fittings, markings and service equipment.
    h.lathe([(-1.19,.55),(-1.10,.67),(.82,.67),(.93,.61)],key='paint',sides=96)
    for y in [-1.10,-.87,-.50,-.12,.26,.64,.87]:h.ring(y,.681,.026,.032,key='silver',sides=72)
    for a in range(0,360,30):
        x,z=polar(.681,a)[::2]; R=rot((0,1,0),90-a)
        for y in [-.83,-.30,.23,.70]:
            h.panel((x,y,z),.25,.43,'paint',R)
        for y in np.linspace(-.94,.73,17):
            for da in [-6,0,6]:
                xx,zz=polar(.696,a+da)[::2]
                h.box((xx,y,zz),(.013,.012,.010),'lamp',rot((0,1,0),90-a-da))
        x,z=polar(.73,a)[::2]
        h.beam((x,-.96,z),(x,.81,z),.009,'frame')
    # Recessed forward head, radial spokes and central navigation mast.
    h.lathe([(.80,.0),(.81,.61)],key='dark',sides=72,closed=False)
    h.ring(.99,.64,.095,.08,key='white',sides=72)
    h.ring(1.05,.57,.018,.035,key='silver',sides=72)
    h.lathe([(.83,.15),(1.18,.18),(1.29,.12)],key='white',sides=32)
    h.ring(1.25,.16,.022,.035,key='silver')
    h.beam((0,1.26,0),(0,1.99,0),.015,'accent',12)
    h.beam((0,1.93,0),(0,2.14,0),.006,'silver',8)
    for a in range(0,360,45):
        x,z=polar(.61,a,1)[::2]
        xx,zz=polar(.15,a)[::2]
        h.beam((xx,1.14,zz),(x,.98,z),.020,'white')
        for rr in [.27,.39,.51]:
            lx,lz=polar(rr,a)[::2]
            h.sphere((lx,.92,lz),.011,'lamp')
        ex,ez=polar(.48,a)[::2]
        h.engine((ex,-1.20,ez),.143,.29,4)
        h.beam((ex,-1.07,ez),(ex*1.23,-1.36,ez*1.23),.014,'silver')
    h.lathe([(-1.25,.15),(-1.40,.18),(-1.53,.09)],key='dark',sides=32)
    h.decal((0,-.13,.705),.42,.17,0)
    if name!='Nauvoo':
        for a in [45,135,225,315]:
            x,z=polar(.74,a)[::2]
            h.box((x,-.49,z),(.22,.35,.21),'dark',rot((0,1,0),45))
            h.pdc((x,.53,z),.045,(0,1,0))
        h.ring(-.88,.714,.022,.042,key='accent',sides=72)
    if name=='Medina':
        for a in [0,120,240]:
            x,z=polar(.66,a)[::2]; xx,zz=polar(.98,a)[::2]
            h.beam((x,-.58,z),(xx,-.58,zz),.035,'frame')
            h.box((xx,-.58,zz),(.19,.42,.16),'paint',rot((0,1,0),90-a))
            h.sphere((xx,-.35,zz),.022,'nav')
    return h


def scopuli():
    h=Hull('Scopuli',(115,121,116),(153,108,63))
    h.box((0,.13,0),(.55,2.54,.42),'frame')
    h.loft([(.15,.42,.36),(.98,.42,.36),(1.32,.35,.29),(1.50,.20,.20)])
    h.box((0,-.84,0),(.72,.56,.61),'paint')
    h.engine((0,-1.16,0),.29,.36)
    for s in [-1,1]:
        h.box((s*.44,.48,-.01),(.22,1.62,.54),'paint')
        h.box((s*.44,-.30,.09),(.19,.21,.22),'dark')
        for y in [-.14,.13,.40,.67,.94]:
            h.box((s*.45,y,.275),(.17,.015,.025),'silver')
            h.box((s*.555,y,.03),(.018,.12,.25),'dark')
        h.beam((s*.29,-.52,-.18),(s*.49,-1.10,-.16),.025,'frame')
        h.rcs((s*.48,1.06,.13),(s,0,0))
        h.sphere((s*.27,-.72,-.30),.14,'silver',(1,1.2,1))
    h.box((0,1.18,.30),(.31,.19,.04),'glass')
    h.decal((0,.60,.369),.33,.14,0)
    h.decal((0,-.84,.314),.31,.14,2)
    for y in [.0,.36,.73]:
        h.beam((-.23,y,.395),(.23,y,.395),.009,'copper')
    h.beam((.12,1.36,.13),(.12,1.77,.13),.004,'silver')
    return h


def scirocco(name='Scirocco'):
    h=Hull(name,(130,137,132),(185,77,39))
    stations=[(-.83,.52,.40),(-.24,.57,.43),(1.34,.58,.43),(1.58,.49,.38)]
    h.loft(stations,cap=False)
    inner=h.loft([(-.82,.41,.30),(1.35,.47,.32),(1.57,.40,.28)],key='dark',cap=False)
    inner.invert()
    h.ring(1.54,.49,.075,.05,key='accent',sides=8)
    h.box((0,.24,-.36),(.65,2.15,.16),'dark')
    h.box((0,-.82,0),(.81,.12,.62),'frame')
    for s in [-1,1]:
        for z in [-.27,.27]:
            h.loft([(-1.40,.23,.22,s*.34,z),(-.89,.25,.23,s*.34,z),
                    (-.61,.20,.18,s*.34,z)],key='paint')
            h.engine((s*.34,-1.40,z),.185,.30,4)
            h.box((s*.34,-1.05,z+(.23 if z>0 else -.23)),(.28,.055,.015),'accent')
        h.box((s*.43,-.59,.42),(.28,.48,.35),'paint')
        h.box((s*.43,-.54,.61),(.20,.16,.013),'glass')
        for y in [-.20,.29,.78]:
            h.box((s*.56,y,-.36),(.18,.35,.16),'dark')
            for dy in [-.18,.18]:h.box((s*.56,y+dy,-.45),(.21,.025,.025),'white')
            h.beam((s*.49,y-.17,-.45),(s*.67,y+.17,-.45),.012,'silver')
        for y in [-.43,.08,.59,1.11]:
            h.pdc((s*.50,y,.38),.049,(0,1,.12))
        for y in [-.37,.22,.81]:
            h.pdc((s*.55,y,-.31),.046,(s*.1,1,0))
        h.box((s*.573,.42,.10),(.014,1.76,.06),'accent')
        h.rcs((s*.51,1.33,.07),(s,0,0),.025)
    h.decal((0,.54,.440),.42,.15,0)
    h.box((.42,-.47,.69),(.15,.16,.10),'dark')
    h.beam((.42,-.48,.69),(.42,.12,.69),.022,'silver')
    return h


def arboghast():
    h=Hull('Arboghast',(142,147,139),(190,166,105))
    h.lathe([(-.38,.53),(-.23,.83),(.12,.99),(.21,1.0),(.27,.80),(.34,.16)],key='paint',sides=96)
    h.ring(.16,1.00,.025,.055,key='silver',sides=96)
    h.ring(-.18,.85,.03,.035,key='dark',sides=72)
    for a in range(0,360,30):
        x,z=polar(.67,a)[::2]
        h.box((x,-.36,z),(.17,.36,.23),'dark',rot((0,1,0),90-a))
        h.beam((x*.58,-.43,z*.58),(x,-.53,z),.022,'silver')
        h.beam((x,-.53,z),(x*1.24,-.03,z*1.24),.018,'frame')
        x,z=polar(.93,a)[::2]
        h.box((x,.04,z),(.029,.026,.013),'nav',rot((0,1,0),90-a))
        h.rcs((x,-.03,z),(math.cos(math.radians(a)),0,math.sin(math.radians(a))),.018)
    h.box((0,.39,0),(.30,.18,.26),'dark')
    h.beam((0,.4,0),(0,.76,0),.016,'silver')
    h.sphere((.28,.39,.12),.13,'white',(.9,.18,1))
    h.beam((.28,.40,.12),(.28,.63,.12),.009,'silver')
    for s in [-1,1]:
        h.box((s*.40,-.46,.09),(.22,.15,.29),'paint')
        h.engine((s*.40,-.52,.09),.10,.16,3)
    h.decal((0,.08,1.005),.32,.12,0)
    # Tilt the wide survey craft for a readable native portrait; it has no
    # missile-like nose and is not artificially stretched to match other ships.
    R=rot((1,0,0),38)
    for parts in h.parts.values():
        for mesh in parts:mesh.apply_transform(R)
    return h


def tynan_production():
    from TynanModel import build_tynan
    return build_tynan(Hull)


BUILDERS={'Tynan':tynan_production,'Morrigan':morrigan,'Canterbury':canterbury,'Knight':knight,
          'Anubis':anubis,'Pella':pella,'Nauvoo':nauvoo,
          'Behemoth':lambda:nauvoo('Behemoth'),'Medina':lambda:nauvoo('Medina'),
          'Scopuli':scopuli,'Scirocco':scirocco,'Hammurabi':lambda:scirocco('Hammurabi'),
          'Arboghast':arboghast,'BashiBazouk':bashi_bazouk}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--ships',nargs='+',choices=list(BUILDERS),default=list(BUILDERS))
    parser.add_argument('--source-dir',type=Path,default=ROOT/'Assets/Models/Source/Fleet')
    parser.add_argument('--native-dir',type=Path,default=ROOT/'Assets/Models',
                        help='Destination for selected native assets; use a staging directory for studies.')
    args=parser.parse_args()
    args.source_dir=args.source_dir.resolve()
    args.native_dir=args.native_dir.resolve()
    report_path=args.source_dir/'build-report.json'
    previous=json.loads(report_path.read_text()).get('models',[]) if report_path.exists() else []
    records={item['id']:item for item in previous}
    for name in args.ships:
        print('BUILD '+name,flush=True)
        hull=BUILDERS[name]()
        source,record=hull.export(args.source_dir)
        # The exact same converter as Razorback; normalises scale and partitions
        # material batches into bounded, frozen native mesh resources.
        args.native_dir.mkdir(parents=True,exist_ok=True)
        output=args.native_dir/(name+'.ship')
        _converter.convert(source,output,'150000')
        record.update({'source':str(source.relative_to(ROOT)),'native':str(output.relative_to(ROOT)),
                       'nativeSha256':hashlib.sha256(output.read_bytes()).hexdigest()})
        records[name]=record
        print('READY '+name+': '+str(record['triangles'])+' triangles',flush=True)
    report_path.write_text(json.dumps({
        'generator':'tools/Build-ExpanseFleet.py','trimesh':trimesh.__version__,
        'method':'Original parametric fan meshes; Pella adapts Martin Taichl CC BY 4.0 geometry. Authored textures, same native conversion as Razorback',
        'limitations':'Fan approximations. Hidden detail and markings interpreted; no production meshes or reference image pixels bundled.',
        'models':[records[name] for name in BUILDERS if name in records]},indent=2)+'\n')


if __name__=='__main__':main()
