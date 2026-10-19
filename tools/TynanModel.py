"""Tynan production-drawing study, authored from Frank Yang's S4 V4 sheets.

Source: https://frankyang.artstation.com/projects/3oAAeo . The ortho explicitly
describes a base model for proportions, not a released final VFX asset. This
is original fan geometry; unseen fittings are interpreted. No artwork pixels
are used in the authored materials. +Y is the bow and -Y the single drive.
The caller owns selective export, staging, preview and validation.
"""
from __future__ import annotations

import math
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import trimesh
from trimesh.visual.material import PBRMaterial
from trimesh.visual.texture import TextureVisuals


def build_tynan(Hull):
    h=Hull('Tynan',(87,94,83),(141,108,48))
    h.materials.clear()

    def linear(c):
        c=np.asarray(c,float)/255
        return np.where(c<=.04045,c/12.92,((c+.055)/1.055)**2.4)

    def patina(rgb,seed):
        r=np.random.default_rng(seed)
        coarse=Image.fromarray(r.normal(0,3.5,(13,17)).astype('float32')).resize((1024,1024),Image.Resampling.BILINEAR)
        fine=r.normal(0,.85,(1024,1024,1))
        a=np.asarray(rgb)[None,None,:]+np.asarray(coarse)[:,:,None]+fine
        im=Image.fromarray(np.uint8(np.clip(a,0,255)))
        d=ImageDraw.Draw(im)
        # Unequal repainting and abrasion patches, not repeating brick cells.
        # Geometry supplies actual seams; the colour map only supplies aged paint.
        for poly,shade in [([(33,70),(282,58),(302,171),(269,209),(41,182)],-5),
                           ([(461,263),(734,240),(769,488),(705,531),(471,476)],4),
                           ([(140,643),(401,631),(452,756),(426,914),(164,900)],-7),
                           ([(789,711),(1017,691),(1023,932),(812,921)],3)]:
            d.polygon(poly,fill=tuple(max(0,min(255,v+shade)) for v in rgb))
        for x,y,width in [(63,177,196),(482,473,221),(168,895,239),(810,914,180)]:
            for j in range(10):
                xx=x+int(r.integers(0,width));yy=y+int(r.integers(-5,5))
                d.line((xx,yy,xx+int(r.integers(3,21)),yy),
                       fill=tuple(min(255,v+int(r.integers(9,25))) for v in rgb),width=1)
                d.line((xx,yy+2,xx+int(r.integers(4,24)),yy+int(r.integers(3,12))),
                       fill=tuple(max(0,v-14) for v in rgb),width=1)
        for _ in range(70):
            x,y=map(int,r.integers(0,1010,2))
            d.line((x,y,x+int(r.integers(2,13)),y+int(r.integers(1,5))),
                   fill=tuple(max(0,v-18) for v in rgb),width=1)
        return im

    def mat(key,rgb,metal=.3,rough=.73,texture=False,glow=None):
        h.materials[key]=PBRMaterial(name='Tynan / '+key,
            baseColorFactor=np.ones(4) if texture else np.r_[linear(rgb),1],
            baseColorTexture=patina(rgb,sum(map(ord,key))+1701) if texture else None,
            metallicFactor=metal,roughnessFactor=rough,
            emissiveFactor=np.asarray(glow or (0,0,0),float))

    mat('paint',(79,84,82),.25,.79,True)
    mat('light',(135,136,128),.22,.80,True)
    mat('armor',(100,104,101),.28,.74,True)
    mat('pod',(46,52,52),.35,.72,True)
    mat('worn',(113,116,108),.35,.67,True)
    mat('warmmetal',(91,75,56),.54,.62)
    mat('primer',(81,49,39),.20,.86,True)
    mat('dark',(30,37,36),.43,.74)
    mat('black',(21,27,28),.12,.85)
    mat('frame',(65,75,72),.65,.60)
    mat('steel',(109,120,118),.69,.49)
    mat('silver',(146,153,147),.75,.44)
    mat('bell',(42,49,51),.61,.64)
    mat('liner',(64,68,66),.59,.67)
    mat('liner_dark',(44,49,48),.62,.72)
    mat('rust',(99,49,36),.28,.84,True)
    mat('ochre',(145,109,42),.21,.81,True)
    mat('white',(176,180,164),.16,.82,True)
    mat('copper',(91,68,45),.55,.69)
    mat('hose',(19,24,25),.07,.9)
    mat('glass',(23,42,44),.42,.22)
    mat('lamp',(227,167,80),0,.5,glow=(.73,.36,.075))
    mat('blue',(43,143,219),.05,.5,glow=(.025,.31,.78))
    mat('nav',(132,202,206),0,.5,glow=(.13,.40,.43))

    def add(m,key='paint',flat=False):
        if flat:m.unmerge_vertices()
        p=m.vertices;t=p[m.faces]
        n=np.zeros_like(p);area=np.cross(t[:,1]-t[:,0],t[:,2]-t[:,0])
        for i in range(3):np.add.at(n,m.faces[:,i],area)
        axis=np.argmax(np.abs(n),axis=1)
        u=np.where(axis==0,p[:,2],p[:,0]);v=np.where(axis==1,p[:,2],p[:,1])
        m.visual=TextureVisuals(uv=np.column_stack((.5+u/1.8,.5+v/2.2)),material=h.materials[key])
        h.parts[key].append(m)
        return m

    # Hull's generic non-paint path assigns a constant UV. Every coloured
    # armour material needs spatial UVs for its authored wear and panel grain.
    # Keep the original cylinder/lathe normals; split only painted flat plating.
    def authored_add(m,key='paint'):
        return add(m,key,flat=key in {'paint','light','armor','rust','ochre','white','pod','worn','primer'})
    h.add=authored_add

    def box(p,size,key='paint',rotation=None):
        return h.box(p,size,key,rotation)

    def beam(a,b,w=.035,key='frame',depth=None):
        a,b=np.asarray(a,float),np.asarray(b,float);v=b-a
        R=trimesh.geometry.align_vectors([0,1,0],v/np.linalg.norm(v))
        return box((a+b)/2,(w,np.linalg.norm(v),depth or w),key,R)

    def cyl(a,b,r,key='steel',sides=20):
        a,b=np.asarray(a,float),np.asarray(b,float);v=b-a
        m=trimesh.creation.cylinder(radius=r,height=np.linalg.norm(v),sections=sides)
        m.apply_transform(trimesh.geometry.align_vectors([0,0,1],v/np.linalg.norm(v)))
        m.apply_translation((a+b)/2)
        return add(m,key)

    def bolt(p,axis=(0,0,1),r=.009,key='steel'):
        p=np.asarray(p,float);axis=np.asarray(axis,float)
        cyl(p-axis*.002,p+axis*.010,r,key,6)

    def pipe(points,r=.012,key='steel',sides=10):
        points=np.asarray(points,float);v=[];f=[]
        for j,p in enumerate(points):
            t=points[min(j+1,len(points)-1)]-points[max(0,j-1)];t/=np.linalg.norm(t)
            a=np.cross(t,[0,1,0] if abs(t[1])<.94 else [1,0,0]);a/=np.linalg.norm(a);b=np.cross(t,a)
            v.extend(p+r*(a*math.cos(theta)+b*math.sin(theta)) for theta in np.arange(sides)*2*math.pi/sides)
        for j in range(len(points)-1):
            for i in range(sides):
                a=j*sides+i;b=j*sides+(i+1)%sides
                f.extend([[a,b,b+sides],[a,b+sides,a+sides]])
        return add(trimesh.Trimesh(vertices=v,faces=f,process=False),key)

    def lathe(profile,p=(0,0,0),key='steel',sides=48,axis=(0,1,0)):
        v=[];f=[]
        for (y0,r0),(y1,r1) in zip(profile[:-1],profile[1:]):
            k=len(v)
            for y,r in [(y0,r0),(y1,r1)]:
                v.extend([[r*math.cos(t),y,r*math.sin(t)] for t in np.arange(sides)*2*math.pi/sides])
            for i in range(sides):
                a=k+i;b=k+(i+1)%sides
                f.extend([[a,b,b+sides],[a,b+sides,a+sides]])
        m=trimesh.Trimesh(vertices=v,faces=f,process=False)
        for (y0,_),(y1,_) in zip(profile[:-1],profile[1:]):
            if abs(y1-y0)>1e-10:
                if y1>y0:m.invert()
                break
        m.apply_transform(trimesh.geometry.align_vectors([0,1,0],axis));m.apply_translation(p)
        return add(m,key)

    def tank(p,r,length,key='silver',axis=(0,1,0)):
        profile=[]
        for a in np.linspace(-math.pi/2,0,7):profile.append((-length/2+r+r*math.sin(a),max(.0005,r*math.cos(a))))
        for a in np.linspace(0,math.pi/2,7):profile.append((length/2-r+r*math.sin(a),max(.0005,r*math.cos(a))))
        lathe(profile,p,key,32,axis)
        for y in [-length*.23,length*.23]:
            lathe([(y-.014,r*1.01),(y-.012,r*1.055),(y+.012,r*1.055),(y+.014,r*1.01)],p,'frame',32,axis)
        for sign in [-1,1]:
            q=np.asarray(p)+np.asarray(axis)*length*.5*sign
            cyl(q,q+np.asarray(axis)*.035*sign,r*.20,'copper',12)

    def panel(p,w,l,key='paint',normal=(0,0,1),inset=True):
        # Panels include a raised clipped frame and a deeply inset centre. Each
        # face is transformed as a unit, preserving correct hard-edge normals.
        R=trimesh.geometry.align_vectors([0,0,1],normal)
        old={k:len(a) for k,a in h.parts.items()}
        box((0,0,0),(w,l,.025),'black')
        if inset:
            # The coloured centre must be above the black backing. Thin grey
            # lips and a shallow recess read as armour, not bright screen frames.
            for x in [-w/2+.009,w/2-.009]:box((x,0,.022),(.018,l,.034),'paint')
            for y in [-l/2+.009,l/2-.009]:box((0,y,.022),(w,.018,.034),'paint')
            box((0,0,.020),(max(.04,w-.052),max(.04,l-.052),.008),key)
            if w>.20 and l>.20:
                for x in [-w/2+.039,w/2-.039]:box((x,0,.026),(.004,l-.07,.005),'frame')
                for y in [-l/2+.039,l/2-.039]:box((0,y,.026),(w-.07,.004,.005),'frame')
        else:box((0,0,.018),(w,l,.025),key)
        for x in [-w/2+.035,w/2-.035]:
            for y in [-l/2+.035,l/2-.035]:bolt((x,y,.038),r=.006)
        for key,items in h.parts.items():
            for m in items[old.get(key,0):]:m.apply_transform(R);m.apply_translation(p)

    def outlined_plate(points,z,depth=.025,key='armor'):
        """Extruded traced XY plate with clipped corners and irregular steps."""
        pts=[np.array(p,float) for p in points]
        def cross(a,b):return a[0]*b[1]-a[1]*b[0]
        if sum(cross(pts[i],pts[(i+1)%len(pts)]) for i in range(len(pts)))<0:pts.reverse()
        ring=list(range(len(pts)));tris=[]
        while len(ring)>3:
            found=False
            for j,b in enumerate(ring):
                a,c=ring[j-1],ring[(j+1)%len(ring)]
                if cross(pts[b]-pts[a],pts[c]-pts[b])<=1e-10:continue
                def inside(k):
                    p=pts[k]
                    return min(cross(pts[b]-pts[a],p-pts[a]),
                               cross(pts[c]-pts[b],p-pts[b]),
                               cross(pts[a]-pts[c],p-pts[c]))>=-1e-10
                if any(inside(k) for k in ring if k not in (a,b,c)):continue
                tris.append([a,b,c]);ring.pop(j);found=True;break
            if not found:raise ValueError('Tynan plate outline is not simple')
        tris.append(ring);n=len(pts)
        v=[[p[0],p[1],z+dz] for dz in [-depth/2,depth/2] for p in pts]
        f=[[c,b,a] for a,b,c in tris]+[[a+n,b+n,c+n] for a,b,c in tris]
        for i in range(n):
            j=(i+1)%n;f.extend([[i,j,j+n],[i,j+n,i+n]])
        return add(trimesh.Trimesh(vertices=v,faces=f,process=False),key,True)

    def mounted(p,normal,build):
        old={key:len(items) for key,items in h.parts.items()}
        build()
        R=trimesh.geometry.align_vectors([0,0,1],normal)
        for key,items in h.parts.items():
            for m in items[old.get(key,0):]:m.apply_transform(R);m.apply_translation(p)

    def service_pocket(p,w,length,kind,normal=(0,0,1),key='paint'):
        """Open bevelled recess with functional contents and a dark inner wall."""
        def build():
            def loop(w,l,c):
                return [(-w/2+c,-l/2),(w/2-c,-l/2),(w/2,-l/2+c),(w/2,l/2-c),
                        (w/2-c,l/2),(-w/2+c,l/2),(-w/2,l/2-c),(-w/2,-l/2+c)]
            shapes=[(loop(w,length,.025),.004),
                    (loop(w-.014,length-.014,.020),.047),
                    (loop(w-.065,length-.065,.016),.052),
                    (loop(w-.065,length-.065,.016),.010)]
            vertices=[[x,y,z] for points,z in shapes for x,y in points]
            front=[];wall=[]
            for j in range(3):
                target=wall if j==2 else front
                for i in range(8):
                    a=j*8+i;b=j*8+(i+1)%8
                    target.extend([[a,b,b+8],[a,b+8,a+8]])
            add(trimesh.Trimesh(vertices=vertices,faces=front,process=False),key,True)
            add(trimesh.Trimesh(vertices=vertices,faces=wall,process=False),'dark',True)
            box((0,0,.007),(w-.05,length-.05,.008),'black')
            for x in [-w*.39,w*.39]:
                for y in [-length*.39,length*.39]:bolt((x,y,.051),r=.005,key='steel')
            if kind=='vent':
                R=trimesh.transformations.rotation_matrix(math.radians(22),[1,0,0])
                for y in np.linspace(-length*.28,length*.28,7):
                    box((0,y,.025),(w-.087,.018,.013),'frame',R)
                for x in [-w*.23,w*.23]:box((x,0,.015),(.009,length-.088,.009),'warmmetal')
            elif kind=='regulator':
                lathe([(0,.047),(.009,.054),(.023,.054),(.027,.037)],(0,.035,.014),'warmmetal',32,(0,0,1))
                cyl((0,.035,.028),(0,.035,.037),.032,'frame',24)
                for a in np.arange(6)*math.pi/3:
                    bolt((.041*math.cos(a),.035+.041*math.sin(a),.043),r=.005)
                pipe([(-.065,-.070,.019),(-.052,-.09,.022),(.044,-.09,.022),
                      (.064,-.066,.020),(.062,-.025,.020)],.007,'copper',10)
                box((-.05,-.05,.019),(.035,.045,.018),'steel')
            elif kind=='relay':
                for x,y,hh in [(-.045,.048,.070),(.045,.025,.112),(-.044,-.065,.037)]:
                    box((x,y,.019),(.054,hh,.020),'frame')
                    for yy in np.arange(-hh*.3,hh*.31,.018):
                        box((x,y+yy,.031),(.033,.006,.004),'steel')
                pipe([(-.071,-.10,.020),(-.071,.097,.021),(.068,.097,.020),
                      (.068,-.083,.020)],.005,'hose',8)
            else:
                for y in [-.052,.045]:
                    lathe([(0,.025),(.010,.031),(.027,.031),(.029,.020),(.008,.018)],
                          (0,y,.011),'steel',24,(0,0,1))
                    pipe([(.027,y,.018),(.062,y+.014,.018),(.063,y+.033,.018)],.006,'copper',10)
        mounted(p,normal,build)

    def latch(p,normal=(0,0,1),length=.08):
        def build():
            box((0,0,.006),(.048,length,.012),'dark')
            box((0,-length*.22,.021),(.038,length*.37,.021),'frame')
            cyl((-.026,length*.20,.020),(.026,length*.20,.020),.012,'steel',12)
            beam((0,-length*.30,.033),(0,length*.13,.035),.011,'steel')
            for y in [-length*.38,length*.38]:bolt((0,y,.015),r=.005)
        mounted(p,normal,build)

    def warm_light(p,normal=(0,0,1)):
        def build():
            box((0,0,.004),(.086,.047,.016),'dark')
            box((0,0,.013),(.060,.027,.010),'warmmetal')
            box((0,0,.020),(.047,.015,.007),'lamp')
            for x in [-.039,.039]:bolt((x,0,.015),r=.005)
            # Opaque visor keeps the luminous surface within a physical fitting.
            box((0,.026,.024),(.095,.010,.026),'frame')
        mounted(p,normal,build)

    def side_plate(points,x,side=-1,depth=.025,key='armor'):
        # Trace in side-elevation Y/Z coordinates, then place the flat shoulder
        # skin with the correct outward-facing hard edges.
        m=outlined_plate([(-side*z,y) for y,z in points],side*x,depth,key)
        m.apply_transform(trimesh.transformations.rotation_matrix(side*math.pi/2,[0,1,0]))
        return m

    def liner_sector(profile,start,end,key):
        """Fitted tapered inner-bell tile with a small physical edge thickness."""
        count=9;angles=np.linspace(start,end,count);v=[];f=[]
        for offset in [0,.006]:
            for y,r in profile:
                v.extend([[(r+offset)*math.cos(a),y,(r+offset)*math.sin(a)] for a in angles])
        layer=len(profile)*count
        for row in range(len(profile)-1):
            for col in range(count-1):
                a=row*count+col;b=a+1;c=b+count;d=a+count
                f.extend([[a,b,c],[a,c,d],[a+layer,c+layer,b+layer],[a+layer,d+layer,c+layer]])
        for row in [0,len(profile)-1]:
            for col in range(count-1):
                a=row*count+col;b=a+1
                f.extend([[a,a+layer,b+layer],[a,b+layer,b]])
        for col in [0,count-1]:
            for row in range(len(profile)-1):
                a=row*count+col;b=a+count
                f.extend([[a,b,b+layer],[a,b+layer,a+layer]])
        return add(trimesh.Trimesh(vertices=v,faces=f,process=False),key)

    # Three non-identical structural rails form an open faceted footprint. The
    # forward halves stay separate, with daylight through the cradle between.
    h.loft([(-1.55,.30,.32,0,-.16),(-1.23,.40,.39,0,-.13),
            (-.51,.37,.36,0,-.10),(.18,.33,.34,0,-.12),
            (1.23,.27,.29,0,-.17),(1.71,.19,.21,0,-.20)],key='dark')
    # Port: long wide grey-green cheek with a beveled overhanging forward tip.
    h.loft([(-.62,.10,.24,-.48,.02),(-.40,.17,.35,-.55,.05),
            (-.10,.25,.45,-.64,.07),(.33,.32,.51,-.71,.09),
            (1.18,.35,.51,-.73,.09),(1.80,.32,.50,-.71,.09),
            (2.25,.28,.43,-.66,.09),(2.53,.15,.29,-.58,.08)],key='paint')
    # Starboard shoulder is shorter, stepped and displaced aft. It is not a
    # mirrored copy: the difference is clear in the five longitudinal angles.
    h.loft([(-.94,.10,.25,.73,-.08),(-.65,.24,.38,.69,-.03),
            (.14,.23,.38,.65,-.03),(.30,.17,.34,.65,-.03),
            (1.16,.17,.34,.65,-.03),(1.32,.24,.42,.65,-.03),
            (1.70,.24,.40,.63,-.04),(1.91,.16,.28,.59,-.06)],key='armor')
    # Rear/ventral longitudinal shell and conspicuous ladder-like open ribs.
    h.loft([(-1.57,.27,.12,0,-.70),(-1.36,.35,.15,0,-.74),
            (-.78,.36,.15,0,-.74),(-.60,.26,.12,0,-.72),
            (.64,.24,.12,0,-.71),(1.28,.23,.12,0,-.70),
            (1.55,.15,.10,0,-.65)],key='paint')
    for sx in [-1,1]:
        beam((sx*.48,-1.46,-.56),(sx*.48,1.94,-.58),.073,'armor')
        for j,y in enumerate(np.linspace(-1.25,1.72,7)):
            # Offset diagonals and rectangular bays retain broad black gaps.
            beam((sx*.48,y,-.59),(sx*.48,y+.30,-.18),.042,'frame')
            beam((sx*.48,y,-.59),(sx*.48,y+.28,-.58),.043,'light')
        beam((sx*.48,1.90,-.58),(sx*.57,2.25,-.11),.073,'light')
    for y in [-1.38,-.54,.20,.98,1.63]:
        beam((-.48,y,-.59),(.48,y,-.59),.047,'frame')
    # Forward triangular fork braces and open top. No cap bridges the prongs.
    for sx in [-1,1]:
        top=2.45 if sx<0 else 2.20
        beam((sx*.53,top,-.12),(sx*.33,1.61,-.43),.062,'light')
        beam((sx*.53,top,-.12),(sx*.12,1.62,.05),.041,'frame')
        beam((sx*.15,1.62,.05),(sx*.33,1.61,-.43),.035,'frame')
        pipe([(sx*.55,top-.03,-.10),(sx*.39,1.86,-.20),(sx*.31,1.57,-.24)],.018,'ochre')

    # Broad traced plates replace three identical rectangular facade bays.
    # The lower shell narrows into a sloping foot; the upper shell has a clipped
    # overhanging shoulder, with a real dark gap between its unequal panels.
    outlined_plate([(-.68,-.37),(-.42,-.20),(-.44,.99),(-.53,1.13),
                    (-1.00,1.13),(-.97,.25)],.621,.035,'armor')
    outlined_plate([(-1.00,1.18),(-.47,1.18),(-.45,1.77),(-.48,2.23),
                    (-.57,2.38),(-.80,2.28),(-.99,1.80)],.618,.029,'light')
    # Narrow secondary plates sit within those broad irregular outlines.
    outlined_plate([(-.90,.30),(-.56,.27),(-.49,.39),(-.49,.87),
                    (-.58,.98),(-.94,.98)],.646,.014,'paint')
    outlined_plate([(-.93,1.28),(-.55,1.28),(-.51,1.69),(-.57,1.80),
                    (-.94,1.77)],.643,.014,'light')
    for p in [(-.91,.92,.658),(-.55,.89,.658),(-.84,.34,.658),
              (-.57,.36,.658),(-.87,1.33,.656),(-.58,1.35,.656),
              (-.88,1.71,.656),(-.59,1.71,.656)]:bolt(p,r=.008)
    # The broad OUTER shoulder is several fitted, stepped skins. Their joints
    # stop and restart around a longitudinal maintenance channel, while each
    # central sheet remains quiet. These wrap the bevel instead of decorating
    # only the front face with another identical rectangular frame.
    side_plate([(.37,-.23),(.51,-.37),(1.02,-.37),(1.23,-.26),
                (1.20,.25),(.96,.34),(.42,.32)],-1.092,key='paint')
    side_plate([(1.26,-.34),(1.89,-.30),(2.20,-.14),(2.10,.28),
                (1.71,.40),(1.28,.33)],-1.045,key='armor')
    side_plate([(.45,-.21),(.60,-.29),(.99,-.29),(1.10,-.21),
                (1.07,.095),(.57,.095)],-1.110,depth=.010,key='worn')
    side_plate([(1.35,-.27),(1.77,-.25),(1.96,-.12),(1.88,.12),
                (1.37,.13)],-1.064,depth=.012,key='paint')
    for yy,xx in [(.47,-1.108),(1.18,-1.108),(1.34,-1.061),(1.91,-1.061)]:
        for zz in [-.18,.21]:bolt((xx,yy,zz),(-1,0,0),.008)
    # A shallow stepped bevel strip joins side skin to the broad front armour.
    for a,b,key in [((-1.02,.42,.37),(-.95,1.08,.47),'paint'),
                    ((-.95,1.25,.48),(-.89,1.76,.49),'worn'),
                    ((-.87,1.82,.48),(-.77,2.23,.43),'armor')]:
        beam(a,b,.090,key,.027)
    # Return layers on the opposite bevel keep the shoulder detailed from the
    # usual three-quarter view as well as from its outer-side orthographic view.
    side_plate([(1.72,-.20),(1.87,-.24),(2.28,-.17),(2.39,.005),
                (2.26,.24),(1.74,.29)],-.381,side=1,depth=.018,key='armor')
    side_plate([(1.81,-.14),(2.20,-.12),(2.27,.015),(2.17,.16),
                (1.82,.19)],-.368,side=1,depth=.011,key='paint')
    for yy,zz in [(1.84,-.13),(2.19,-.10),(1.85,.16),(2.16,.13)]:
        bolt((-.354,yy,zz),(1,0,0),.007)
    beam((-.350,1.78,-.17),(-.350,1.78,.22),.009,'dark',.005)
    for yy in [1.90,2.07]:
        # Short abrasion/primer breaks along the exposed folded seam.
        side_plate([(yy,-.225),(yy+.055,-.222),(yy+.07,-.197),(yy,-.196)],
                   -.354,side=1,depth=.004,key='primer')
    for y,l,key in [(-.46,.58,'paint'),(.55,.71,'paint'),(1.50,.47,'armor')]:
        panel((.65,y,.357 if y<.22 else (.315 if y<1.1 else .398)),.31 if y<1.1 else .37,l,key)
        panel((.893 if y<.22 else (.827 if y<1.1 else .895),y,-.03),.57 if y<1.1 else .68,l,key,(1,0,0))
    # Bold faded red slab at port shoulder; limited pale panels and ochre lines.
    outlined_plate([(-.90,1.31),(-.64,1.31),(-.57,1.39),(-.60,1.73),
                    (-.90,1.70)],.657,.011,'rust')
    for x in [-.917,-.581]:box((x,1.51,.668),(.011,.39,.007),'white')
    for y in [-.42,.19,1.20,2.21]:
        xc,width=(-.52,.25) if y<0 else ((-.68,.44) if y<.5 else (-.71,.46))
        box((xc,y,.645), (width,.022,.018),'frame')
    box((-.70,.46,.663),(.34,.018,.007),'ochre')
    box((.913,1.52,-.05),(.021,.34,.47),'white')
    # Local medium-scale relief: an interrupted service run down the shoulder,
    # one recessed regulator and unequal overlapping repair plates. Broad hull
    # areas stay clear; there is no uniform grid of decorative surface boxes.
    outlined_plate([(-.96,.30),(-.88,.31),(-.88,.97),(-.93,1.04),
                    (-.98,.98)],.658,.009,'primer')
    outlined_plate([(-.55,1.30),(-.47,1.36),(-.47,1.76),(-.51,1.78),
                    (-.56,1.62)],.648,.010,'primer')
    outlined_plate([(-.88,1.84),(-.70,1.81),(-.58,1.96),(-.60,2.18),
                    (-.80,2.24),(-.88,2.08)],.644,.018,'worn')
    service_pocket((-.905,.64,.670),.115,.43,'vent',key='paint')
    service_pocket((-.665,.71,.661),.245,.255,'regulator',key='armor')
    service_pocket((-.725,2.015,.661),.195,.28,'relay',key='worn')
    for p in [(-.535,.52,.662),(-.535,.91,.662),(-.940,1.46,.646),(-.520,2.13,.636)]:
        latch(p,length=.068)
    for a,b in [((-.92,1.12,.648),(-.70,1.12,.648)),
                ((-.68,1.12,.648),(-.53,1.12,.648)),
                ((-.97,1.21,.642),(-.77,1.21,.642))]:
        beam(a,b,.006,'dark',.004)
    service_pocket((-1.124,.75,.235),.145,.50,'vent',(-1,0,0),'paint')
    service_pocket((-1.080,1.61,.255),.15,.37,'connector',(-1,0,0),'paint')
    for yy,xx in [(.47,-1.135),(1.03,-1.135),(1.39,-1.090),(1.82,-1.090)]:
        latch((xx,yy,.23),(-1,0,0),.064)
    # Small dark louvres follow the sloping lower foot, rather than a new fin.
    for j in range(7):
        yy=-.36+j*.043;xx=-.63-j*.030
        box((xx,yy,.382+j*.029),(.075,.018,.018),'dark')
    # Port inboard underside is rusty industrial structure, not green armour.
    panel((-.397,.78,.08),.67,1.68,'rust',(1,0,0))
    for y in [-.08,.51,1.11,1.67]:
        box((-.326,y,.06),(.038,.056,.69),'frame')
        pipe([(-.310,y,.39),(-.310,y+.12,.26),(-.310,y+.12,-.21)],.013,'copper')
    # Central stacked mechanism does not plug the open forward fork.
    for y,w,l,d in [(1.43,.24,.23,.17),(1.11,.30,.28,.20),(.72,.38,.32,.22),(.29,.45,.39,.23)]:
        box((.02,y,-.02),(w,l,d),'dark')
        panel((.02,y,d/2-.012),w*.85,l*.77,'frame')
        for x in [-w*.32,w*.32]:bolt((x,y+.06,d/2+.038),r=.008)
    for x in [-.14,.17]:
        pipe([(x,1.51,.08),(x,1.14,.15),(x,.65,.22),(x,-.16,.27),(x,-.64,.19)],.020,'steel',14)
    for y in [.08,.59,1.03]:
        box((.03,y,.24),(.42,.045,.09),'frame')

    # Connected hardware lives against the cavity walls, preserving the open
    # longitudinal channel. Hydraulic actuators, bolted pump heads and harness
    # trays have clear mounting points on the inner port shoulder.
    # Three different assemblies replace the repeated identical pump blocks.
    tank((-.223,1.20,-.12),.060,.56,'warmmetal')
    for yy in [1.03,1.36]:
        box((-.279,yy,-.12),(.11,.040,.22),'frame')
        cyl((-.225,yy,-.19),(-.225,yy,-.05),.015,'steel',12)
    pipe([(-.223,1.51,-.12),(-.193,1.57,-.16),(-.24,1.65,-.20)],.013,'copper')
    box((-.263,.68,-.09),(.13,.18,.35),'dark')
    for z in [-.20,-.07,.06]:
        cyl((-.188,.68,z),(-.139,.68,z),.037,'warmmetal',24)
        cyl((-.143,.68,z),(-.127,.68,z),.027,'steel',20)
        bolt((-.122,.68,z),(1,0,0),.013,'steel')
        pipe([(-.23,.58,z),(-.18,.52,z),(-.18,.42,z-.025)],.009,'hose')
    service_pocket((-.185,.18,-.07),.26,.30,'relay',(1,0,0),'frame')
    for yy in [.01,.36]:box((-.265,yy,-.07),(.17,.030,.32),'frame')
    pipe([(-.26,.02,.10),(-.23,-.12,.13),(-.24,-.30,.13)],.015,'hose')
    warm_light((-.303,.99,.17),(1,0,0))
    for z in [-.27,-.21,-.15]:
        pipe([(-.31,-.39,z),(-.30,-.12,z),(-.30,1.47,z),(-.36,1.73,z-.06)],.011,'steel')
        for y in [-.20,.31,.88,1.40]:box((-.29,y,z),(.043,.035,.040),'frame')
    for p in [(-.21,.43,.15),(-.19,1.07,.11)]:
        tank(p,.046,.42,'steel')
        for yy in [-.13,.13]:box(np.asarray(p)+[0,yy,-.035],(.14,.029,.063),'frame')
        pipe([np.asarray(p)+[0,-.24,0],np.asarray(p)+[.07,-.29,.03],
              np.asarray(p)+[.07,-.42,.03]],.010,'copper')
    # Starboard interior has a different valve bank and tall vent, not matching
    # left-side equipment mirrored across the craft.
    panel((.39,.86,-.10),.35,.70,'frame',(-1,0,0))
    for yy in [.63,.86,1.09]:
        for z in [-.19,-.01]:
            cyl((.31,yy,z),(.26,yy,z),.037,'steel',20)
            bolt((.251,yy,z),(-1,0,0),.017,'copper')
    for yy in [.12,.23,.34]:
        pipe([(.34,yy,-.28),(.27,yy,-.33),(.27,yy-.23,-.33),(.19,yy-.31,-.25)],.012,'hose')
    for sx in [-1,1]:
        a=np.array([sx*.41,-.38,-.28]);b=np.array([sx*.31,.26,-.43])
        cyl(a,b,.025,'steel',20)
        cyl(a,a+(b-a)*.48,.040,'frame',24)
        box(a,(.14,.09,.13),'frame')

    # Prominent independent torpedo-bay casing on the starboard face. A long
    # chamfered pod, external bands and mounting legs differ from the main wall.
    h.loft([(-.54,.18,.23,.66,.57),(-.39,.23,.29,.66,.57),
            (.96,.23,.29,.66,.57),(1.21,.19,.25,.66,.57),
            (1.35,.12,.18,.66,.57)],key='pod')
    for y in [-.35,.15,.69,1.12]:
        box((.66,y,.873),(.37,.044,.036),'frame')
        box((.905,y,.57),(.043,.045,.44),'frame')
        beam((.42,y,.46),(.65,y,.30),.055,'frame')
    panel((.66,.36,.872),.35,1.16,'pod')
    panel((.903,.29,.56),.39,1.18,'pod',(1,0,0))
    panel((.903,1.035,.56),.34,.24,'frame',(1,0,0))
    # Differing access plates, handle/hinge clusters and continuous external
    # straps replace the three identical dark rectangles of the first study.
    panel((.980,.14,.56),.24,.40,'dark',(1,0,0))
    panel((.980,.63,.56),.27,.23,'pod',(1,0,0))
    for y in [-.25,.47,.88]:
        box((.66,y,.950),(.39,.043,.027),'frame')
        box((.994,y,.56),(.028,.043,.46),'frame')
        for z in [.39,.72]:bolt((1.012,y,z),(1,0,0),.011)
    for j in range(6):box((.998,.99-.09+j*.032,.56),(.016,.012,.23),'steel')
    box((.66,1.15,.950),(.35,.021,.012),'ochre')
    box((.66,-.36,.950),(.36,.023,.012),'ochre')
    for x in [.51,.81]:pipe([(x,-.44,.85),(x,-.29,.97),(x,1.05,.97),(x,1.22,.80)],.009,'steel')
    # Deep end fittings and continuous stamped bands make this one substantial
    # industrial casing rather than several thin boxes attached to a wall.
    service_pocket((.66,1.352,.57),.205,.255,'connector',(0,1,0),'pod')
    service_pocket((1.034,.35,.56),.28,.33,'regulator',(1,0,0),'pod')
    service_pocket((.66,.27,.974),.235,.36,'vent',key='pod')
    for yy in [-.25,.47,.88]:
        beam((.84,yy,.958),(.973,yy,.823),.043,'frame',.022)
        beam((.973,yy,.823),(1.014,yy,.76),.043,'frame',.022)
        for p in [(.85,yy,.964),(1.021,yy,.74)]:
            bolt(p,(0,0,1) if p[0]<1 else (1,0,0),.009)
    # A dedicated hinge strip has pivot barrels and three unlike latch groups.
    box((.829,.34,.983),(.033,1.14,.019),'dark')
    for yy,ll in [(-.08,.16),(.34,.22),(.78,.11)]:
        cyl((.842,yy-ll/2,.993),(.842,yy+ll/2,.993),.014,'steel',16)
        for end in [-1,1]:box((.831,yy+end*(ll/2+.018),.981),(.048,.026,.028),'frame')
    for yy in [-.08,.59,1.02]:latch((.494,yy,.948),length=.087)
    for yy in [-.15,.63]:latch((1.024,yy,.405),(1,0,0),.093)
    pipe([(1.021,-.24,.72),(1.046,-.17,.71),(1.045,.09,.71),
          (1.020,.17,.73)],.007,'warmmetal',10)

    # Offset main airlock on +Z, below the fork. Its smaller door is mounted
    # above the long faded banner panel, as labelled in the production ortho.
    h.loft([(-1.30,.27,.12,.13,.63),(-1.20,.33,.16,.13,.65),
            (-.40,.33,.16,.13,.65),(-.28,.28,.12,.13,.64)],key='paint')
    panel((.13,-.80,.835),.56,.79,'paint')
    lathe([(-.02,.17),(0,.215),(.06,.215),(.075,.173),(.10,.173)],(.13,-.23,.51),'ochre',8,(0,0,1))
    box((.13,-.23,.622),(.25,.30,.022),'black')
    for x in [.073,.187]:box((x,-.23,.642),(.108,.245,.017),'paint')
    for x in [-.14,.40]:
        beam((x,-.40,.66),(x,-.03,.66),.022,'steel')
    box((.13,-.43,.69),(.61,.055,.26),'frame')
    for x in [-.17,.43]:pipe([(x,-.43,.70),(x,-.51,.94),(x,-.33,.94)],.010,'ochre')
    # Visible behind-frame and grouped latch blocks keep the banner/airlock a
    # separately mounted structure. The doorway gasket sits behind a raised lip.
    for x in [-.236,.497]:
        beam((x,-1.25,.79),(x,-.40,.79),.038,'frame')
        for yy in [-1.17,-.56]:
            beam((x,yy,.70),(x,yy,.87),.030,'frame')
            latch((x,yy,.884),length=.075)
    for y in [-1.26,-.40]:beam((-.236,y,.79),(.497,y,.79),.039,'frame')
    outlined_plate([(-.16,-.425),(.42,-.425),(.47,-.47),(.44,-.52),
                    (-.18,-.52),(-.21,-.47)],.846,.026,'worn')
    for xx in [-.025,.285]:latch((xx,-.22,.665),length=.064)
    for xx in [-.015,.275]:
        box((xx,-.23,.661),(.009,.28,.017),'hose')
    warm_light((.13,-.424,.89))
    # Warm lamps are deep inside the dark cavities and around the service door.
    for p in [(-.36,.05,.32),(.32,.31,.38),(.13,-.44,.81),(-.27,-1.30,.22),(.43,-1.20,.31)]:
        box(p,(.082,.037,.055),'black')
        box(np.asarray(p)+[0,0,.029],(.059,.019,.009),'lamp')

    # Lower ring and flight-deck bay: broad segmented cradle, octagonal glazed
    # recesses and off-axis side pods. A complete smooth hull barrel is avoided.
    lathe([(-1.56,.50),(-1.51,.64),(-1.36,.65),(-1.29,.54)],key='frame',sides=12)
    for module,a in enumerate([15,75,135,195,255,315]):
        rad=math.radians(a);r=.62
        x,z=r*math.cos(rad),r*math.sin(rad)
        R=trimesh.transformations.rotation_matrix(-rad,[0,1,0])
        radial=np.array([math.cos(rad),0,math.sin(rad)])
        tangent=np.array([-math.sin(rad),0,math.cos(rad)])
        centre=radial*.77+np.array([0,-1.12,0])
        if module==1:
            # A genuinely open, triangular-braced gap between solid modules.
            a0=radial*.67+[0,-1.32,0]-tangent*.16
            b0=radial*.67+[0,-1.32,0]+tangent*.16
            top=radial*.64+[0,-.86,0]
            beam(a0,b0,.029,'frame');beam(a0,top,.033,'frame');beam(b0,top,.033,'frame')
            pipe([radial*.48+[0,-.89,0],radial*.55+[0,-1.12,0],
                  radial*.47+[0,-1.39,0]],.018,'hose')
            warm_light(radial*.57+[0,-.96,0],radial)
            continue
        box((x,-1.12,z),(.23 if module==0 else .21,.44 if module==0 else .40,.33 if module==0 else .31),
            'light' if module==0 else 'paint',R)
        for yy in [-1.30,-.96]:
            box((x*1.02,yy,z*1.02),(.22,.043,.31),'frame',R)
        if module==0:
            panel(centre,.27,.34,'worn',radial)
            for offset in [-.10,.10]:latch(centre+tangent*offset+radial*.043,radial,.073)
            pipe([centre-tangent*.12+[0,-.11,0],centre-tangent*.14+[0,-.24,0],
                  radial*.56+[0,-1.48,0]],.011,'warmmetal')
            continue
        if module==2:
            # One transverse ribbed filter, not another identical bottle pair.
            tank(centre,.080,.29,'frame',tangent)
            for yy in [-.10,.10]:
                beam(centre+tangent*yy+[0,-.10,0],centre+tangent*yy+[0,.10,0],.025,'steel')
            for t in [-.10,-.06,-.02,.02,.06,.10]:
                lathe([(t-.006,.082),(t-.004,.093),(t+.004,.093),(t+.006,.082)],
                      centre,'steel',32,tangent)
            pipe([centre+tangent*.18,centre+tangent*.22+[0,-.15,0],
                  radial*.49+[0,-1.53,0]+tangent*.10],.015,'copper')
            continue
        if module==4:
            service_pocket(centre,.28,.31,'vent',radial,'pod')
            for offset in [-.10,.10]:
                pipe([centre+tangent*offset+[0,-.19,0],
                      radial*.65+tangent*offset+[0,-1.39,0],
                      radial*.44+tangent*offset+[0,-1.53,0]],.010,'steel')
            continue
        # Remaining two modules carry different compact canister/valve groups.
        for sign in [-1,1]:
            p=centre+tangent*sign*.075
            tank(p,.046 if module==3 else .039,.25 if sign<0 else .20,'steel')
            for yy in [-.065,.065]:
                beam(p+[0,yy,0]-tangent*.065,p+[0,yy,0]+tangent*.065,.019,'frame')
            pipe([p+[0,-.15,0],p+[0,-.20,0]-radial*.07,
                  radial*.52+[0,-1.52,0]+tangent*sign*.06],.009,'copper')
        box(centre+radial*.04+[0,.18,0],(.13,.055,.22),'frame',R)
        for offset in [-.07,.07]:
            q=centre+radial*.11+tangent*offset+[0,.18,0]
            cyl(q,q+radial*.025,.022,'steel',12)
            bolt(q+radial*.028,radial,.012,'copper')
    # Flight-deck viewing slit points toward the side between the lower racks.
    box((-.52,-1.17,.47),(.27,.19,.24),'dark')
    panel((-.52,-1.17,.605),.24,.14,'frame')
    box((-.52,-1.17,.637),(.18,.052,.012),'glass')
    for x in [-.565,-.475]:box((x,-1.17,.647),(.009,.056,.009),'steel')
    # Breaching pod is separate on the back/port side, rather than mirrored.
    h.loft([(-1.31,.18,.19,-.64,-.52),(-1.19,.24,.24,-.64,-.52),
            (-.67,.24,.24,-.64,-.52),(-.54,.18,.18,-.64,-.52)],key='white')
    panel((-.64,-.93,-.775),.37,.52,'white',(0,0,-1))
    for y in [-1.15,-.70]:box((-.64,y,-.78),(.43,.035,.028),'frame')
    for y in [-1.20,-.65]:beam((-.42,y,-.42),(-.70,y,-.51),.060,'frame')

    # Aft engine neck and a single wide bell. Feed tanks and support rods are
    # distinct from the silhouette of the main industrial hull.
    lathe([(-1.48,.42),(-1.63,.37),(-1.79,.33),(-1.94,.28)],key='primer',sides=48)
    for y,r in [(-1.59,.39),(-1.77,.35),(-1.93,.30)]:
        lathe([(y-.025,r),(y-.023,r+.035),(y+.023,r+.035),(y+.025,r)],key='frame',sides=64)
    lathe([(-1.94,.25),(-2.07,.28),(-2.22,.39),(-2.38,.48),(-2.58,.55),
           (-2.72,.56),(-2.75,.55),(-2.75,.49),(-2.61,.48),(-2.39,.39),
           (-2.18,.24)],key='bell',sides=96)
    for y,r in [(-2.26,.42),(-2.41,.50),(-2.58,.567),(-2.72,.577)]:
        lathe([(y-.014,r),(y-.012,r+.019),(y+.012,r+.019),(y+.014,r)],key='frame',sides=80)
    lathe([(-2.73,.50),(-2.76,.54),(-2.78,.54),(-2.78,.50)],key='steel',sides=96)
    lathe([(-2.20,.25),(-2.20,.001)],key='blue',sides=96)
    for a in np.arange(10)*2*math.pi/10:
        c,s=math.cos(a),math.sin(a)
        pipe([(c*.32,-1.70,s*.32),(c*.42,-1.85,s*.42),(c*.45,-2.07,s*.45),
              (c*.49,-2.29,s*.49)],.018,'copper',12)
        cyl((c*.43,-1.91,s*.43),(c*.44,-2.02,s*.44),.032,'steel',16)
    for a in np.arange(24)*2*math.pi/24:
        c,s=math.cos(a),math.sin(a)
        pipe([(c*.29,-2.07,s*.29),(c*.40,-2.24,s*.40),(c*.50,-2.44,s*.50),
              (c*.55,-2.67,s*.55)],.008,'steel',8)
    for j,(angle,radius,length) in enumerate([(18,.072,.36),(64,.093,.23),(121,.062,.42),
                                              (169,.10,.24),(226,.077,.33),(287,.087,.22)]):
        a=math.radians(angle)
        radial=np.array([math.cos(a),0,math.sin(a)]);tangent=np.array([-math.sin(a),0,math.cos(a)])
        centre=radial*(.44 if j%2 else .50)+np.array([0,-1.96,0])
        tank(centre,radius,length,'silver' if j in [1,3] else 'steel')
        box(centre+[0,.13,0],(.13,.045,.13),'frame')
        pipe([centre+[0,.17,0],centre+[0,.23,0]-radial*.06,
              radial*.28+[0,-1.63,0]],.013,'steel')
        pipe([centre+[0,-.17,0],centre+[0,-.22,0]+tangent*.05,
              radial*.38+[0,-2.20,0]+tangent*.05],.010,'copper')
    # Clamped nozzle mounting flange, asymmetric upper-neck manifolds and
    # physical internal rings remain legible when the emissive drive is off.
    for angle in [25,105,196,283]:
        a=math.radians(angle);radial=np.array([math.cos(a),0,math.sin(a)])
        tangent=np.array([-math.sin(a),0,math.cos(a)])
        p=radial*.37+[0,-1.76,0]
        beam(p+[0,.13,0],p+radial*.025+[0,-.17,0],.067,'frame',.035)
        for yy in [-1.66,-1.83]:
            for side in [-1,1]:bolt(radial*.411+tangent*.035*side+[0,yy,0],radial,.010)
    for angle,yy in [(38,-1.63),(147,-1.72),(254,-1.59)]:
        a=math.radians(angle);radial=np.array([math.cos(a),0,math.sin(a)])
        tangent=np.array([-math.sin(a),0,math.cos(a)])
        p=radial*.46+[0,yy,0]
        box(p,(.13,.115,.13),'dark')
        cyl(p+radial*.06,p+radial*.09,.038,'warmmetal',24)
        for side in [-1,1]:
            pipe([p+tangent*.066*side,p+tangent*.12*side+[0,-.08,0],
                  radial*.40+tangent*.12*side+[0,-1.96,0]],.012,'steel')
    for yy,rr in [(-2.205,.255),(-2.28,.295),(-2.36,.345)]:
        lathe([(yy-.009,rr),(yy-.006,rr+.012),(yy+.006,rr+.012),(yy+.009,rr)],key='warmmetal',sides=64)
    # Twelve fitted liner sectors make the entire bell interior physical. Their
    # narrow recessed joins terminate at annular carriers; no unsupported rods
    # hang across the mouth. Internal structure is interpreted engineering.
    liner_profile=[(-2.735,.481),(-2.61,.464),(-2.50,.425),
                   (-2.39,.375),(-2.28,.296),(-2.21,.245)]
    for index in range(12):
        a=index*math.pi/6
        liner_sector(liner_profile,a+.014,a+math.pi/6-.014,
                     'liner_dark' if index in [2,5,8,11] else 'liner')
        middle=a+math.pi/12
        radial=np.array([math.cos(middle),0,math.sin(middle)])
        for yy,rr in [(-2.65,.467),(-2.48,.412)]:
            # Recessed countersunk retaining heads follow the inner wall.
            bolt(radial*rr+[0,yy,0],-radial,.006,key='frame')
    for yy,rr in [(-2.64,.468),(-2.48,.416),(-2.305,.306)]:
        lathe([(yy-.013,rr-.007),(yy-.013,rr+.007),(yy+.013,rr+.007),
               (yy+.013,rr-.007),(yy-.013,rr-.007)],key='liner_dark',sides=96)
    # Recessed injector face and two concentric raised carriers. The existing
    # blue throat sits ahead of it only when the user enables drive emission.
    cyl((0,-2.187,0),(0,-2.198,0),.239,'dark',64)
    for rr in [.075,.153,.214]:
        lathe([(-2.206,rr-.006),(-2.206,rr+.006),(-2.218,rr+.006),
               (-2.218,rr-.006),(-2.206,rr-.006)],key='frame',sides=80)
    for a in np.arange(16)*math.pi/8:
        c,s=math.cos(a),math.sin(a)
        cyl((c*.183,-2.207,s*.183),(c*.183,-2.220,s*.183),.010,'black',12)
        if round(a/(math.pi/8))%2==0:
            beam((c*.084,-2.220,s*.084),(c*.145,-2.220,s*.145),.012,'warmmetal',.009)
    # Unequal manifolds bridge the nozzle mount and tank saddles. Their fittings
    # are concentrated at real feed junctions, not distributed as surface dots.
    for angle,yy,span in [(9,-1.72,.24),(131,-1.61,.17),(245,-1.81,.20)]:
        a=math.radians(angle);radial=np.array([math.cos(a),0,math.sin(a)])
        tangent=np.array([-math.sin(a),0,math.cos(a)])
        p=radial*.405+[0,yy,0]
        beam(p-tangent*span/2,p+tangent*span/2,.068,'frame',.055)
        for offset in [-span*.32,span*.32]:
            q=p+tangent*offset+radial*.040
            lathe([(0,.021),(.015,.027),(.036,.027),(.040,.018)],q,'steel',20,radial)
            pipe([q+radial*.05,q+radial*.08+[0,-.08,0],
                  radial*.43+tangent*offset+[0,-2.03,0]],.010,'copper',12)
        for offset in [-span*.43,span*.43]:
            bolt(p+tangent*offset+radial*.040,radial,.009,'steel')
    # Four splayed structural stabilisers are long open V braces, not fins.
    for a in [45,135,225,315]:
        t=math.radians(a);c,s=math.cos(t),math.sin(t)
        beam((c*.63,-1.31,s*.63),(c*.76,-2.35,s*.76),.033,'frame')
        beam((c*.42,-1.61,s*.42),(c*.76,-2.35,s*.76),.025,'steel')
        box((c*.76,-2.35,s*.76),(.065,.12,.065),'frame')

    # Equipment is arranged in connected banks: pipe trays, valve manifolds,
    # louvres and strapped bottles attach to the frame, not random free greebles.
    for sx in [-1,1]:
        for j in range(3):
            x=sx*(.29+j*.065)
            pipe([(x,-1.40,.31),(x,-1.12,.39),(x,-.56,.42),(x,-.19,.35),
                  (x,.39,.31)],.010,'steel')
        for y in [-1.15,-.65,-.22,.26]:
            beam((sx*.22,y,.40),(sx*.48,y,.40),.031,'frame')
        for yy in [.30,.85,1.35]:
            panel((sx*.41,yy,-.29),.24,.24,'black')
            for k in range(5):box((sx*.41,yy-.075+k*.035,-.243),(.17,.012,.009),'steel')
    # Deep forward services: asymmetrically located dual pressure reservoirs.
    for p in [(-.18,.81,-.34),(.22,.40,-.40)]:
        tank(p,.083,.66,'frame')
        for yy in [-.20,.20]:box(np.asarray(p)+[0,yy,.065],(.19,.035,.07),'steel')
        pipe([np.asarray(p)+[0,-.37,0],np.asarray(p)+[.12,-.43,.09],
              np.asarray(p)+[.12,-.62,.09]],.015,'copper')
    for y in [-.85,-.22,.43,1.07]:
        panel((0,y,-.88),.39,.39,'paint',(0,0,-1))
    for x in [-.17,.17]:
        pipe([(x,-1.40,-.92),(x,-.80,-.94),(x,.97,-.94),(x,1.34,-.80)],.015,'steel')
    for y in [-1.15,-.53,.12,.72]:
        box((0,y,-.947),(.40,.031,.028),'frame')

    # Bow mount and angular mast, small nozzles and clearly asymmetric grapples.
    box((-.63,2.58,.09),(.24,.12,.25),'frame')
    h.pdc((-.63,2.70,.09),.22,axis=(0,1,0))
    for p,tip in [((-.83,.45,-.28),(-1.45,1.55,-.64)),
                  ((.81,-.18,-.17),(1.39,.80,-.65)),
                  ((.31,1.71,-.54),(.56,2.92,-.88))]:
        p=np.asarray(p);tip=np.asarray(tip);v=tip-p
        cyl(p,p+v*.39,.023,'frame',14)
        cyl(p+v*.29,tip,.010,'steel',12)
        cyl(p-v*.04,p+v*.08,.047,'dark',18)
        for s in [-1,1]:beam(tip,tip+[s*.062,.075,.03],.010,'steel')
    for p,l in [((-.61,2.50,.34),.55),((.57,2.20,-.08),.42),((.16,1.74,-.41),.57),
                ((-.49,-.37,-.72),.78)]:
        p=np.asarray(p);cyl(p,p+[0,l,0],.006,'steel',10)
        cyl(p,p+[0,.11,0],.014,'frame',12)
    for p,axis in [((-.86,2.19,-.11),(-1,0,0)),((.83,1.91,-.10),(1,0,0)),
                   ((-.72,-1.28,.30),(-1,0,0)),((.63,-1.39,-.40),(1,0,0))]:
        p=np.asarray(p);a=np.asarray(axis)
        box(p,(.12,.15,.12),'frame')
        lathe([(0,.024),(.035,.033),(.052,.035),(.052,.023),(.009,.017)],p+a*.07,'steel',16,axis)
    for p in [(-.62,2.57,.23),(.58,2.25,.16),(-.53,-1.48,.45)]:
        box(p,(.04,.04,.05),'nav')

    # Authored faded OPA emblem and TYNAN stencil; no sampled concept pixels.
    tex=Image.new('RGB',(512,1024),(52,58,55));d=ImageDraw.Draw(tex)
    for y in [49,309,681,956]:d.line((0,y,511,y),fill=(36,44,41),width=2)
    d.polygon([(23,70),(86,53),(95,462),(35,475)],fill=(46,52,49))
    d.polygon([(348,522),(495,538),(498,933),(375,916)],fill=(59,64,59))
    d.ellipse((108,115,404,411),outline=(144,140,113),width=10)
    d.line((153,376,257,125,359,375),fill=(144,140,113),width=10)
    d.line((183,290,329,290),fill=(144,140,113),width=10)
    d.line((104,275,405,275),fill=(144,140,113),width=6)
    try:font=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',58)
    except OSError:font=ImageFont.load_default()
    d.text((113,635),'TYNAN',font=font,fill=(114,121,112))
    rr=np.random.default_rng(199)
    for _ in range(170):
        x,y=map(int,rr.integers([0,0],[500,1010]));d.line((x,y,x+int(rr.integers(3,24)),y+1),fill=(45,53,49))
    for y in [146,279,373,655,676]:
        x=int(rr.integers(113,330))
        d.line((x,y,x+int(rr.integers(26,74)),y+3),fill=(52,58,55),width=3)
    h.materials['markings']=PBRMaterial(name='Tynan / authored faded markings',baseColorFactor=np.ones(4),
        baseColorTexture=tex,metallicFactor=.17,roughnessFactor=.86)
    m=trimesh.Trimesh(vertices=[[-.10,-1.15,.887],[.36,-1.15,.887],[.36,-.46,.887],[-.10,-.46,.887]],
        faces=[[0,1,2],[0,2,3]],process=False)
    m.visual=TextureVisuals(uv=[[0,0],[1,0],[1,1],[0,1]],material=h.materials['markings'])
    h.parts['markings'].append(m)
    return h
