"""Original Bashi Bazouk study from Lee Fitzgerald's final S5 drawings.

Reference: https://www.artstation.com/artwork/xYGE4O (final exterior and ortho).
This is authored fan geometry, not a scan, extracted asset or official model.
Reference pixels are never sampled into textures. Hidden fittings are interpreted.
The slender white ship in the drawing is the Razorback size comparison.

Coordinates: +Y bow, -Y drive, +Z crane side. Dimensions are proportional units.
Only build_bashi(Hull) is public; the caller owns export and preview paths.
"""
from __future__ import annotations

import math
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import trimesh
from trimesh.visual.material import PBRMaterial
from trimesh.visual.texture import TextureVisuals


def build_bashi(Hull):
    h = Hull('BashiBazouk', (52, 58, 62), (137, 80, 49))
    h.materials.clear()
    rng = np.random.default_rng(250512)

    def linear(rgb):
        c = np.asarray(rgb, float) / 255
        return np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)

    def surface(rgb, seed, white=False):
        # Authored shallow plate grain and isolated chipped edges. Large seams
        # are modelled separately; there is no repeated black tile checkerboard.
        r = np.random.default_rng(seed)
        a = np.asarray(rgb)[None, None, :] + r.normal(0, 1.0, (1024, 1024, 1))
        im = Image.fromarray(np.uint8(np.clip(a, 0, 255)))
        d = ImageDraw.Draw(im)
        for j in range(8):
            y = 128*j
            d.line((0, y, 1023, y), fill=tuple(max(0,v-9) for v in rgb), width=1)
            for i in range(4):
                x = i*256 + (j%2)*64
                d.line((x,y,x,y+128),fill=tuple(max(0,v-6) for v in rgb),width=1)
                if r.random() < .55:
                    d.line((x+7,y+3,x+int(r.integers(14,42)),y+3),
                           fill=tuple(min(255,v+(8 if white else 15)) for v in rgb),width=1)
        for _ in range(90):
            x,y = map(int,r.integers(0,1008,2))
            d.line((x,y,x+int(r.integers(2,11)),y),fill=tuple(max(0,v-17) for v in rgb))
        return im

    def material(key, rgb, metal=.35, rough=.66, texture=False, glow=None):
        h.materials[key] = PBRMaterial(name='Bashi / '+key,
            baseColorFactor=np.ones(4) if texture else np.r_[linear(rgb),1],
            baseColorTexture=surface(rgb,sum(map(ord,key)),key=='white') if texture else None,
            metallicFactor=metal,roughnessFactor=rough,
            emissiveFactor=np.asarray(glow or (0,0,0),float))

    material('paint', (43,50,54), .26,.76,True)
    material('armor',(55,63,67),.29,.70,True)
    material('black',(21,26,30),.19,.83,True)
    material('dark',(31,37,41),.52,.61)
    material('frame',(73,80,83),.72,.49)
    material('silver',(141,151,153),.78,.39)
    material('steel',(95,109,113),.78,.42)
    material('white',(201,202,192),.18,.72,True)
    material('copper',(88,61,47),.38,.78,True)
    material('rustedge',(124,89,65),.43,.70)
    material('joint',(55,44,37),.48,.75)
    material('bell',(42,49,53),.63,.61)
    material('hose',(18,23,26),.08,.87)
    material('glass',(20,39,43),.45,.23)
    material('blue',(37,135,221),.08,.43,glow=(.025,.37,.91))
    material('lamp',(239,203,135),.03,.4,glow=(.8,.51,.16))
    material('nav',(164,211,222),.05,.45,glow=(.2,.53,.62))

    # The upper ivory diagonal in the SIDE ortho is the silhouette of a real
    # Y/Z wedge, not a stripe running diagonally across the X/Y front face.
    # Remap only the slab and its attached fittings from their construction
    # coordinates; the rear spine, crane, collar and drive never enter this map.
    rake_active=False

    def rake_positions(vertices):
        p=np.asarray(vertices,float).copy()
        selected=p[:,1]>=1.50
        if not np.any(selected):return p
        y=p[selected,1];z=p[selected,2]
        original_front=np.interp(y,[1.50,1.70,1.78,2.63,2.82],
                                   [.595,.435,.435,.435,.355])
        rear=np.interp(y,[1.50,1.70,2.63,2.82],[-.605,-.585,-.585,-.505])
        # A hard knee at 1.78 and a 31-degree rake to the upper cap. The last
        # station retains the existing short terminal chamfer and full width.
        front=np.interp(y,[1.50,1.78,2.63,2.82],[.595,.595,.080,.015])
        fraction=np.clip((z-rear)/(original_front-rear),0,1)
        p[selected,2]=z+(front-original_front)*fraction
        return p

    def add(mesh,key='paint',flat=False):
        if rake_active:
            mesh.vertices=rake_positions(mesh.vertices)
        if flat:
            mesh.unmerge_vertices()
        p=mesh.vertices
        tri=p[mesh.faces]
        area=np.cross(tri[:,1]-tri[:,0],tri[:,2]-tri[:,0])
        n=np.zeros_like(p)
        for i in range(3):np.add.at(n,mesh.faces[:,i],area)
        axis=np.argmax(np.abs(n),axis=1)
        u=np.where(axis==0,p[:,2],p[:,0])
        v=np.where(axis==1,p[:,2],p[:,1])
        mesh.visual=TextureVisuals(uv=np.column_stack((.5+u/1.8,.5+v/2.4)),material=h.materials[key])
        h.parts[key].append(mesh)
        return mesh

    # The shared Hull helper gives non-paint materials constant UVs. Route all
    # coloured plating through the authored mapping, retaining rounded normals
    # on the physical tanks, nozzles and pipes. Geometry and palette are fixed.
    def authored_add(mesh,key='paint'):
        return add(mesh,key,flat=key in {'paint','armor','black','white','copper'})
    h.add=authored_add

    def box(p,size,key='paint',rotation=None):
        # Hull.box already separates each flat face and bevel at its sharp edge.
        return h.box(p,size,key,rotation)

    def beam(a,b,width=.04,key='copper',depth=None):
        a,b=np.asarray(a,float),np.asarray(b,float)
        v=b-a
        R=trimesh.geometry.align_vectors([0,1,0],v/np.linalg.norm(v))
        return box((a+b)/2,(width,np.linalg.norm(v),depth or width),key,R)

    def cylinder(a,b,r,key='silver',sides=20):
        a,b=np.asarray(a,float),np.asarray(b,float)
        v=b-a
        m=trimesh.creation.cylinder(radius=r,height=np.linalg.norm(v),sections=sides)
        m.apply_transform(trimesh.geometry.align_vectors([0,0,1],v/np.linalg.norm(v)))
        m.apply_translation((a+b)/2)
        return add(m,key)

    def pipe(points,r=.011,key='steel',sides=10):
        """Continuous tube with shared curved vertices, including elbow bends."""
        points=np.asarray(points,float)
        verts=[]
        for j,p in enumerate(points):
            t=points[min(j+1,len(points)-1)]-points[max(0,j-1)]
            t/=np.linalg.norm(t)
            helper=np.array([0.,1.,0.]) if abs(t[1])<.94 else np.array([1.,0.,0.])
            a=np.cross(t,helper);a/=np.linalg.norm(a);b=np.cross(t,a)
            verts.extend(p+r*(np.cos(theta)*a+np.sin(theta)*b) for theta in np.arange(sides)*2*math.pi/sides)
        faces=[]
        for j in range(len(points)-1):
            for i in range(sides):
                a=j*sides+i;b=j*sides+(i+1)%sides
                faces.extend([[a,b,b+sides],[a,b+sides,a+sides]])
        return add(trimesh.Trimesh(vertices=verts,faces=faces,process=False),key)

    def lathe(profile,center=(0,0,0),key='steel',sides=64,axis=(0,1,0)):
        # Duplicate vertices at every meridian shoulder; smooth around the bell
        # but keep its collar, lip and inner lining crisply separated.
        verts=[];faces=[]
        for (y0,r0),(y1,r1) in zip(profile[:-1],profile[1:]):
            offset=len(verts)
            for y,r in [(y0,r0),(y1,r1)]:
                verts.extend([[r*math.cos(t),y,r*math.sin(t)] for t in np.arange(sides)*2*math.pi/sides])
            for i in range(sides):
                a=offset+i;b=offset+(i+1)%sides
                faces.extend([[a,b,b+sides],[a,b+sides,a+sides]])
        m=trimesh.Trimesh(vertices=verts,faces=faces,process=False)
        # Open meridians have no meaningful enclosed volume or centre of mass.
        # For this winding, a decreasing-Y outer wall faces radially outward;
        # reverse increasing-Y profiles, leaving explicitly wound flat disks.
        for (y0,_),(y1,_) in zip(profile[:-1],profile[1:]):
            if abs(y1-y0)>1e-10:
                if y1>y0:m.invert()
                break
        m.apply_transform(trimesh.geometry.align_vectors([0,1,0],axis))
        m.apply_translation(center)
        return add(m,key)

    def bolt(p,axis=(0,0,1),r=.012,key='silver'):
        p=np.asarray(p,float);a=np.asarray(axis,float)
        cylinder(p-a*.003,p+a*.010,r,key,6)

    def face(points,key='white'):
        pts=np.asarray(points,float)
        if np.cross(pts[1]-pts[0],pts[2]-pts[0])[2]<0:
            pts=pts[::-1]
        faces=[[0,i,i+1] for i in range(1,len(pts)-1)]
        # Paint patches are flat polygons above actual hull faces, not floating
        # rectangles or textured copies of the production drawings.
        return add(trimesh.Trimesh(vertices=pts,faces=faces,process=False),key,True)

    def plate_frame(p,w,l,edge=.018,depth=.018,key='paint'):
        """Clipped rectangular ring, including the deep inside walls."""
        def loop(w,l,c):
            return [(-w/2+c,-l/2),(w/2-c,-l/2),(w/2,-l/2+c),(w/2,l/2-c),
                    (w/2-c,l/2),(-w/2+c,l/2),(-w/2,l/2-c),(-w/2,-l/2+c)]
        outside=loop(w,l,min(.029,w*.18,l*.12))
        inside=loop(w-edge*2,l-edge*2,min(.021,w*.13,l*.09))
        verts=[];faces=[]
        for zz in [-depth/2,depth/2]:
            for shape in [outside,inside]:
                verts.extend([[p[0]+x,p[1]+y,p[2]+zz] for x,y in shape])
        for i in range(8):
            j=(i+1)%8
            for a,b,c,d in [(i,j,j+16,i+16),(i+8,i+24,j+24,j+8),
                            (i+16,j+16,j+24,i+24),(i,i+8,j+8,j)]:
                faces.extend([[a,b,c],[a,c,d]])
        add(trimesh.Trimesh(vertices=verts,faces=faces,process=False),key,True)

    def bevel_panel(x,y,z,w,l,key='armor',side=1):
        # A dark bay sits behind two nested perimeter frames; a broad panel is
        # not just another small extruded box stacked along the hull.
        box((x,y,z),(w,l,.014),'black')
        plate_frame((x,y,z+side*.018),w,l,.022,.025,key)
        box((x,y,z+side*.009),(w-.064,l-.064,.009),'black')
        plate_frame((x,y,z+side*.016),w-.093,l-.093,.004,.006,'frame')
        for dx in [-w/2+.027,w/2-.027]:
            for dy in [-l/2+.027,l/2-.027]:bolt((x+dx,y+dy,z+side*.033),(0,0,side),.006,'steel')

    def rounded_tank(p,r,length,key='silver',axis=(0,1,0)):
        # Meridian hemisphere ends keep the dense engine plumbing convincingly
        # round in silhouette, without the angular icosphere approximation.
        profile=[]
        for theta in np.linspace(-math.pi/2,0,7):
            profile.append((-length/2+r+r*math.sin(theta),max(.0005,r*math.cos(theta))))
        for theta in np.linspace(0,math.pi/2,7):
            profile.append((length/2-r+r*math.sin(theta),max(.0005,r*math.cos(theta))))
        lathe(profile,p,key,32,axis)
        R=trimesh.geometry.align_vectors([0,1,0],axis)
        for yy in [-length*.24,length*.24]:
            lathe([(yy-.014,r*1.015),(yy-.012,r*1.07),(yy+.012,r*1.07),(yy+.014,r*1.015)],p,'frame',32,axis)
        pp=np.asarray(p)
        for sign in [-1,1]:
            q=pp+np.asarray(axis)*length*.5*sign
            cylinder(q,q+np.asarray(axis)*.035*sign,r*.22,'steel',12)

    # MAIN HABITABLE SLAB. Forward five-sided shoulders, broad flat side plates
    # and a conspicuously offset rear machinery spine, measured against orthos.
    rake_active=True
    section=[(-.72,-1),(.72,-1),(1,-.72),(1,.72),(.72,1),(-.72,1),(-1,.72),(-1,-.72)]
    h.loft([(-.12,.405,.54,0,-.035),(.16,.46,.60,0,-.005),
            (1.50,.46,.60,0,-.005),(1.70,.46,.51,0,-.075),
            (1.78,.46,.51,0,-.075),(2.63,.46,.51,0,-.075),
            (2.82,.40,.43,0,-.075)],section,'black')
    # Slightly stepped broad face rather than one extrusion or rectangular box.
    for side in [-1,1]:
        zsign=side
        for row,(yc,lh) in enumerate([(.36,.65),(1.11,.75),(2.17,1.00)]):
            if side>0 and row==2:
                # Stay between the new knee and the terminal bevel; every
                # frame wall, bolt and recessed backing follows the same rake.
                yc,lh=2.205,.81
            z=(.603 if yc<1.5 else .44) if side>0 else (-.615 if yc<1.5 else -.59)
            for col,x in enumerate([-.172,.172]):
                bevel_panel(x,yc,z,.317,lh,'paint' if (row+col)%3 else 'armor',side)
                if row in [0,1,2]:
                    box((x,yc-lh*.28,z+side*.021),(.19,.047,.011),'paint')
                    box((x-.06,yc-lh*.28,z+side*.030),(.04,.008,.005),'steel')
                    for yy in [yc-lh*.20,yc-lh*.13]:
                        box((x+.047,yy,z+side*.019),(.073,.006,.005),'frame')
        # A few stout partial-length copper clamps, with dark gasket outlines,
        # cross the larger bays exactly as structural reinforcement should.
        for yy,length,xc in [(.02,.62,0),(.72,.48,-.055),(1.52,.60,0),(2.71,.56,-.02)]:
            # This existing shoulder clamp already sits on the retained lower
            # plane. Keep it there while the upper face retreats behind it.
            rake_active=not(side>0 and yy==1.52)
            zz=(.651 if yy<1.6 else .488) if side>0 else -.667
            box((xc,yy,zz), (length,.054,.029),'joint')
            box((xc,yy,zz+side*.014),(length-.025,.034,.021),'copper')
            for dx in [-length*.40,length*.40]:bolt((xc+dx,yy,zz+side*.031),(0,0,side),.009,'steel')
        rake_active=True
        # Upper shoulder panels on the chamfered flanks; the long edge is a
        # sequence of independent removable plates with visible black reveals.
        for y,l in [(0.27,.55),(.88,.60),(1.42,.42),(1.91,.42),(2.4,.43)]:
            d=.60 if y<1.6 else .51
            zc=-.005 if y<1.6 else -.075
            R=trimesh.transformations.rotation_matrix(side*math.radians(45),[0,1,0])
            box((side*.409,y,zc+.51*d),(.11,l,.035),'armor',R)
    # Fore/aft edges retain strong dimensional seams and stout service clamps.
    for sx in [-1,1]:
        for y,l in [(.36,.65),(1.11,.75),(2.17,1.00)]:
            depth=.71 if y<1.55 else .61
            box((sx*.465,y,-.065),(.025,l,depth),'paint')
            box((sx*.483,y,-.065),(.014,l-.075,depth-.075),'black')
            for z in [-.065-depth*.44,-.065+depth*.44]:
                beam((sx*.505,y-l*.45,z),(sx*.505,y+l*.45,z),.019,'armor')
            for yy in [y-l*.45,y+l*.45]:
                beam((sx*.505,yy,-.065-depth*.44),(sx*.505,yy,-.065+depth*.44),.019,'armor')
            for z in [-.28,.16]:bolt((sx*.517,y+l*.38,z),(sx,0,0),.008,'steel')
        for y in [.0,.47,1.48,2.73]:
            box((sx*.487,y,-.07),(.045,.055,.76),'copper')
            box((sx*.512,y,-.07),(.012,.028,.67),'rustedge')

    # Ivory identification trim is fitted to the raked face. Its X position is
    # constant: the diagonal appears in SIDE silhouette because the hull slopes.
    # Split exactly at the hard knee so the strip cannot bridge through it.
    for yy,length in [(1.745,.070),(2.205,.850)]:
        box((.264,yy,.477),(.105,length,.012),'white')
    face([[.2115,1.710,.481],[.3165,1.710,.481],
          [.3165,1.48,.644],[.2115,1.48,.644]],'white')
    # Thin return wraps over the actual clipped shoulder into the side cheek.
    # It is a fitted strip on the chamfer, not a second painted diagonal.
    for yy0,yy1 in [(1.78,2.20),(2.20,2.63)]:
        face([[.334,yy0,.439],[.463,yy0,.296],
              [.463,yy1,.296],[.334,yy1,.439]],'white')
    for y,l in [(1.22,.47),(.68,.48),(.20,.38)]:
        box((.264,y,.643),(.105,l,.009),'white')
    for sx in [-1,1]:
        for y,l in [(2.62,.29),(1.52,.22),(.19,.33)]:
            box((sx*.484,y,-.065),(.011,l,.72 if y<1.6 else .59),'white')
        box((sx*.337,2.62,-.588),(.11,.29,.009),'white')
        for y,l in [(.21,.38),(.68,.48),(1.19,.47)]:
            box((sx*.28,y,-.631),(.11,l,.010),'white')
    rake_active=False

    # Rear dark spine is visibly separate from the slab, wider at its foot.
    h.loft([(-1.40,.22,.125,0,-.72),(-1.23,.27,.16,0,-.73),
            (.04,.25,.15,0,-.73),(.40,.20,.12,0,-.73),
            (1.45,.20,.12,0,-.73),(1.64,.25,.15,0,-.73),
            (2.83,.25,.15,0,-.73)],key='paint')
    for yc in np.arange(-1.10,2.75,.26):
        box((0,yc,-.891),(.37,.22,.013),'armor')
        box((0,yc+.09,-.902),(.29,.009,.006),'black')
    for x in [-.185,.185]:
        pipe([(x,-1.26,-.92),(x,-.72,-.925),(x,1.30,-.925),(x,2.64,-.90)],.010,'steel')
    for y in [-1.18,-.48,.40,1.4,2.67]:
        for x in [-.20,.20]:box((x,y,-.90),(.07,.055,.07),'frame')

    # Narrow gap and adapter before the drive cage. Sculpted stepped equipment
    # cheeks leave the centre of the bus and copper support struts exposed.
    h.loft([(-1.45,.30,.31),(-1.30,.36,.34),(-.95,.34,.32),(-.79,.28,.26)],key='dark')
    for sx in [-1,1]:
        h.loft([(-1.47,.105,.37,sx*.45,-.12),(-1.30,.15,.40,sx*.45,-.12),
                (-.88,.15,.40,sx*.45,-.12),(-.70,.095,.33,sx*.45,-.12)],key='paint')
        for yy in [-1.28,-1.05,-.84]:
            box((sx*.608,yy,-.13),(.018,.17,.55),'armor')
            for zz in [-.33,.08]:bolt((sx*.622,yy+.05,zz),(sx,0,0),.008)
        box((sx*.57,-.93,.23),(.10,.50,.10),'white')
        pipe([(sx*.32,-1.46,.27),(sx*.33,-.97,.29),(sx*.30,-.74,.25)],.025,'copper',14)
        beam((sx*.25,-1.37,.40),(sx*.30,-.58,.48),.07,'frame')
    for yy in [-1.25,-1.08,-.90]:
        box((0,yy,.344),(.48,.12,.045),'paint')
        for x in np.linspace(-.18,.18,7):box((x,yy,.369),(.018,.083,.010),'black')

    # UNIVERSAL DOCKING BRIDGE. A substantial side-to-side collar and one
    # extended octagonal adapter on the crane face, with an inset divided hatch.
    h.loft([(-.70,.48,.54),(-.56,.62,.65),(-.07,.62,.65),(.04,.48,.54)],key='frame')
    for sx in [-1,1]:
        box((sx*.56,-.30,-.04),(.22,.61,.89),'paint')
        box((sx*.674,-.27,-.04),(.024,.43,.62),'armor')
        for yy in [-.45,-.12]:
            for z in [-.24,.17]:
                box((sx*.693,yy,z),(.016,.09,.07),'black')
        for yy in [-.60,-.02]:box((sx*.57,yy,.17),(.25,.058,.43),'white')
        # Paired exposed vertical tanks on each outer collar edge.
        for z in [-.39,.37]:
            rounded_tank((sx*.73,-.30,z),.060,.61,'silver')
            pipe([(sx*.73,-.59,z),(sx*.79,-.65,z),(sx*.80,-.65,z*.50),
                  (sx*.71,-.72,z*.50)],.012,'copper')
    # Octagonal port axis projects toward the rail; stacked rim depth is real.
    port=(0,-.315,.67)
    lathe([(-.025,.34),(.0,.40),(.07,.40),(.08,.35),(.18,.35),(.22,.38),
           (.26,.38),(.27,.305),(.22,.295)],port,'copper',8,axis=(0,0,1))
    lathe([(.245,.365),(.26,.375),(.28,.375),(.29,.307)],port,'silver',8,axis=(0,0,1))
    box((0,-.315,.938),(.48,.48,.025),'black')
    for x in [-.111,.111]:box((x,-.315,.957),(.209,.394,.019),'armor')
    box((0,-.315,.969),(.014,.39,.009),'dark')
    for x in [-.09,.09]:box((x,-.315,.973),(.030,.074,.018),'steel')
    for a in np.arange(8)*math.pi/4+math.pi/8:
        bolt((.341*math.cos(a),-.315+.341*math.sin(a),.952),(0,0,1),.014)
    for x in [-.38,.38]:
        box((x,-.25,.73),(.10,.37,.14),'black')
        box((x,-.25,.812),(.077,.12,.024),'lamp')
    # A short lip and safety handrails around the working threshold.
    box((0,-.72,.80),(.81,.055,.33),'frame')
    for x in [-.40,.40]:
        pipe([(x,-.69,.66),(x,-.73,1.01),(x,-.52,1.01)],.010,'silver')

    # CRANE RAIL. Single large faceted bow on +Z, not bilateral wings or a ring.
    # The two parallel flanges and central running slot make its function legible.
    arc=[(2.82,.01),(2.73,.38),(2.45,.82),(2.04,1.12),(1.55,1.36),
         (1.00,1.47),(.43,1.45),(-.13,1.30),(-.65,1.08),(-1.18,.79),(-1.48,.42)]
    for x in [-.105,.105]:
        for (y0,z0),(y1,z1) in zip(arc[:-1],arc[1:]):
            beam((x,y0,z0),(x,y1,z1),.070,'copper',.056)
            # A worn running edge and darker inner guide track.
            beam((x+.022*np.sign(x),y0,z0+.015),(x+.022*np.sign(x),y1,z1+.015),.013,'rustedge',.014)
    for j,(yy,zz) in enumerate(arc):
        beam((-.14,yy,zz),(.14,yy,zz),.038,'copper')
        for sx in [-1,1]:
            box((sx*.123,yy,zz),(.023,.115,.12),'joint')
            box((sx*.141,yy,zz),(.013,.078,.082),'copper')
            bolt((sx*.147,yy,zz),(sx,0,0),.018)
        if j in [1,3,5,7,9]:
            box((0,yy,zz+.027),(.11,.12,.018),'black')
    # Sparse braces follow the drawing and preserve large triangular openings.
    for a,b in [((0,1.54,.47),(0,1.55,1.36)),((0,.52,.62),(0,1.00,1.47)),
                ((0,-.70,.45),(0,-.13,1.30)),((0,-1.40,.32),(0,-1.18,.79))]:
        beam(a,b,.058,'copper')
    # Folded articulated boom, lying along the outer rail during flight.
    pivot=np.array([0,1.96,1.20]);elbow=np.array([0,1.18,1.57]);wrist=np.array([0,.50,1.51])
    for a,b,w in [(pivot,elbow,.10),(elbow,wrist,.074)]:
        for sx in [-1,1]:beam(a+[sx*.07,0,0],b+[sx*.07,0,0],w,'copper',.058)
        cylinder(a+[.14,0,-.045],b+[.14,0,-.045],.023,'silver',18)
        cylinder(a+[.14,0,-.045],(a+b)/2+[.14,0,-.045],.036,'dark',20)
    for p in [pivot,elbow,wrist]:
        cylinder(p+[-.16,0,0],p+[.16,0,0],.073,'frame',24)
        for sx in [-1,1]:
            cylinder(p+[sx*.16,0,0],p+[sx*.177,0,0],.050,'rustedge',20)
            bolt(p+[sx*.181,0,0],(sx,0,0),.019)
    box(wrist+[0,-.10,.02],(.24,.17,.15),'dark')
    for sx in [-1,1]:
        beam(wrist+[sx*.11,-.11,0],wrist+[sx*.19,-.30,-.03],.042,'steel')
        beam(wrist+[sx*.19,-.30,-.03],wrist+[sx*.08,-.35,-.08],.036,'silver')
    # Black cable follows the arc, then forms a flexible droop at the carriage.
    pipe([[.16,y,z-.035] for y,z in arc],.014,'hose',12)
    pipe([[.17,1.95,1.2],[.19,1.65,1.46],[.19,1.42,1.47],[.16,1.18,1.57]],.017,'hose',12)

    # AFT DRIVE CAGE. Narrow upper frame fans into a wide square base, with open
    # diagonal braces: the largest footprint belongs to machinery, not armour.
    upper_y,lower_y=-1.45,-2.62
    upper_r,lower_r=.66,1.10
    corners=[(-1,-1),(1,-1),(1,1),(-1,1)]
    for i,(sx,sz) in enumerate(corners):
        a=(sx*upper_r,upper_y,sz*upper_r)
        b=(sx*lower_r,lower_y,sz*lower_r)
        nxt=corners[(i+1)%4]
        c=(nxt[0]*lower_r,lower_y,nxt[1]*lower_r)
        d=(nxt[0]*upper_r,upper_y,nxt[1]*upper_r)
        beam(a,b,.069,'copper')
        beam(b,c,.074,'copper')
        beam(a,d,.055,'copper')
        # Secondary polished inner bracing, with very few crossing members.
        beam((sx*.27,-1.60,sz*.27),b,.026,'steel')
        beam((sx*.91,-2.61,sz*.91),(sx*.51,-2.96,sz*.51),.045,'steel')
        beam((sx*.51,-2.96,sz*.51),(nxt[0]*.51,-2.96,nxt[1]*.51),.035,'frame')
        box(b,(.17,.13,.17),'copper')
        box((b[0],b[1]-.075,b[2]),(.12,.025,.12),'steel')
        for dx in [-.04,.04]:bolt((b[0]+dx,b[1]-.091,b[2]),(0,-1,0),.015)
        # Midspan welded bracket and a small blue work light, not solid panels.
        mid=(np.asarray(a)+np.asarray(b))/2
        box(mid,(.10,.09,.10),'frame')
        box((sx*.94,-2.63,sz*.94),(.10,.045,.10),'nav')
    for sy in [-1,1]:
        # Orthogonal slender perimeter tension rods remain outside drive bell.
        beam((-1.10,-2.62,sy*1.10),(-.66,-1.45,sy*.66),.015,'silver')
        beam((1.10,-2.62,sy*1.10),(.66,-1.45,sy*.66),.015,'silver')

    # Central reactor, recessed bell, multiple concentric rings and feed lines.
    lathe([(-1.43,.25),(-1.53,.32),(-1.77,.34),(-1.92,.30)],key='dark',sides=64)
    for y,r in [(-1.55,.33),(-1.70,.35),(-1.84,.33)]:
        lathe([(y-.02,r),(y-.018,r+.035),(y+.018,r+.035),(y+.02,r)],key='silver',sides=64)
    lathe([(-1.86,.27),(-2.03,.30),(-2.17,.38),(-2.35,.51),(-2.59,.64),
           (-2.79,.71),(-2.85,.70),(-2.85,.635),(-2.73,.63),(-2.54,.56),
           (-2.33,.43),(-2.12,.26)],key='bell',sides=96)
    for y,r in [(-2.19,.41),(-2.38,.55),(-2.59,.66),(-2.79,.72)]:
        lathe([(y-.020,r),(y-.015,r+.025),(y+.015,r+.025),(y+.02,r)],key='steel',sides=80)
    lathe([(-2.82,.654),(-2.845,.675),(-2.867,.675),(-2.88,.645)],key='silver',sides=96)
    # Recessed luminous throat: no opaque cone or plume masking physical detail.
    lathe([(-2.17,.285),(-2.17,.001)],key='blue',sides=96)
    for theta in np.arange(32)*2*math.pi/32:
        c,s=math.cos(theta),math.sin(theta)
        pipe([(c*.31,-2.02,s*.31),(c*.39,-2.20,s*.39),(c*.54,-2.42,s*.54),
              (c*.64,-2.68,s*.64),(c*.68,-2.80,s*.68)],.008,'frame',8)
    for theta in np.arange(8)*math.pi/4:
        c,s=math.cos(theta),math.sin(theta)
        pipe([(c*.30,-1.66,s*.30),(c*.49,-1.89,s*.49),(c*.60,-2.17,s*.60),
              (c*.70,-2.44,s*.70)],.022,'copper',12)
        cylinder((c*.59,-2.13,s*.59),(c*.62,-2.25,s*.62),.044,'frame',16)

    # Distinct pairs of polished spheres and long pressure bottles fill only
    # the gaps around the bell, leaving every cage face transparent.
    for sx in [-1,1]:
        for sz in [-1,1]:
            rounded_tank((sx*.55,-1.90,sz*.55),.155,.47,'silver',axis=(0,.97,.24*sz))
            rounded_tank((sx*.77,-2.32,sz*.51),.105,.29,'silver',axis=(.34*sx,.94,0))
            pipe([(sx*.50,-1.63,sz*.54),(sx*.70,-1.76,sz*.62),(sx*.71,-2.13,sz*.66),
                  (sx*.80,-2.25,sz*.53)],.014,'steel',12)
            beam((sx*.37,-1.61,sz*.51),(sx*.77,-2.48,sz*.75),.027,'frame')
            for y in [-1.77,-2.04]:
                box((sx*.56,y,sz*.54),(.31,.038,.045),'frame')
    # Four discrete service racks, each consisting of paired pressure bottles,
    # a dark manifold, bent feeds and proper saddles. These sit in the empty
    # cardinal gaps above the bell, clear of the diagonal cage and main tanks.
    for a in np.arange(4)*math.pi/2:
        radial=np.array([math.cos(a),0,math.sin(a)])
        tangent=np.array([-math.sin(a),0,math.cos(a)])
        R=trimesh.transformations.rotation_matrix(-a,[0,1,0])
        centre=radial*.65+np.array([0,-1.82,0])
        box(centre-radial*.065,(.11,.43,.36),'dark',R)
        for sign in [-1,1]:
            p=centre+tangent*sign*.105+radial*.075
            rounded_tank(p,.061,.43,'steel')
            for yy in [-1.98,-1.67]:
                beam(p+[0,yy-p[1],0]-tangent*.075,
                     p+[0,yy-p[1],0]+tangent*.075,.024,'frame')
            # Coiled feed follows the saddle before reaching the main manifold.
            pipe([p+[0,-.24,0],p+[0,-.29,0]+radial*.055,
                  centre+tangent*sign*.14+[0,-.34,0]+radial*.12,
                  centre+tangent*sign*.14+[0,-.43,0],
                  centre+[0,-.49,0]-radial*.05],.012,'silver',12)
            pipe([p+[0,.24,0],p+[0,.28,0]-radial*.09,
                  centre+[0,.31,0]-radial*.13],.014,'copper',12)
        block=centre+np.array([0,-.34,0])
        box(block,(.18,.13,.32),'frame',R)
        for offset in [-.10,0,.10]:
            q=block+tangent*offset+radial*.104
            cylinder(q,q+radial*.035,.023,'steel',12)
            bolt(q+radial*.04,radial,.013,'silver')
        # Broad stamped bracket with protruding triangular mounting gussets.
        for yy in [-1.64,-2.02]:
            beam(centre+[0,yy-centre[1],0]-tangent*.22,
                 centre+[0,yy-centre[1],0]+tangent*.22,.033,'copper')
        for sign in [-1,1]:
            beam(radial*.39+[0,-1.52,0]+tangent*sign*.17,
                 centre+[0,-.19,0]+tangent*sign*.20,.030,'frame')
        pipe([radial*.33+[0,-1.46,0],radial*.47+[0,-1.60,0],
              radial*.49+[0,-1.90,0],radial*.64+[0,-2.13,0],
              radial*.74+[0,-2.32,0]],.024,'hose',14)
    for sx in [-1,1]:
        box((sx*.76,-1.65,-.14),(.23,.28,.34),'paint')
        for y in [-1.74,-1.66,-1.58]:box((sx*.885,y,-.14),(.015,.028,.24),'black')
        pipe([(sx*.76,-1.51,-.13),(sx*.83,-1.38,-.23),(sx*.62,-1.16,-.25)],.022,'hose',14)

    # SERVICE HARDWARE ON THE MAIN SLAB: six exposed silver cylinders, cable
    # trays, shallow louvres and access handles at a scale visible in the panel.
    rake_active=True
    for sx in [-1,1]:
        for j in range(5):
            y=.12+j*.24
            rounded_tank((sx*.53,y,-.47),.043,.21,'silver')
            cylinder((sx*.53,y-.13,-.47),(sx*.53,y-.15,-.47),.023,'copper',12)
            pipe([(sx*.53,y-.15,-.47),(sx*.60,y-.15,-.43),(sx*.60,y-.17,-.33)],.008,'steel')
        pipe([(sx*.50,.02,-.58),(sx*.55,.18,-.62),(sx*.55,1.38,-.62),(sx*.49,1.58,-.51)],.017,'steel',12)
        for y in [.31,.77,1.25]:box((sx*.55,y,-.62),(.08,.034,.07),'frame')
    # Recessed side equipment banks break up the forward flank without false
    # weapons or unsupported giant structures.
    for sx in [-1,1]:
        for y in [1.85,2.30]:
            box((sx*.486,y,-.11),(.016,.30,.30),'black')
            for j in range(7):box((sx*.50,y-.115+j*.037,-.11),(.018,.016,.245),'steel')
        for y in [.48,.96]:
            box((sx*.49,y,.12),(.024,.23,.17),'black')
            for z in [.065,.17]:beam((sx*.52,y-.065,z),(sx*.52,y+.065,z),.013,'silver')
    # Fine dorsal pipes in the shadow of the crane and three long aft conduits.
    for j in range(3):
        x=-.29+j*.065
        pipe([(x,-.02,.63),(x,.21,.66),(x,1.39,.66),(x,1.50,.65),
              (x,1.78,.49),(x,2.64,.48)],.009,'steel',10)
        for y in [.30,.90,1.32,2.12]:box((x,y,.669 if y<1.4 else .49),(.029,.025,.024),'frame')
    rake_active=False
    # Bow antenna cluster and small RCS service blocks, with physically open
    # nozzle holes on three axes. No combat turrets are invented for this tug.
    box((-.24,2.85,-.30),(.24,.09,.26),'frame')
    for x,ylen,z in [(-.29,.79,-.32),(-.22,.64,-.27),(-.17,.49,-.34)]:
        cylinder((x,2.87,z),(x,2.87+ylen,z),.006,'silver',10)
        cylinder((x,2.86,z),(x,3.00,z),.014,'frame',12)
    for sx in [-1,1]:
        for y in [2.57,-1.30]:
            p=np.array([sx*.49,y,-.38])
            box(p,(.15,.16,.17),'frame')
            for axis in [(sx,0,0),(0,1 if y>0 else -1,0),(0,0,-1)]:
                a=np.asarray(axis,float)
                lathe([(0,.028),(.04,.036),(.057,.038),(.057,.025),(.012,.019)],p+a*.07,'silver',16,a)
        box((sx*.47,2.79,-.20),(.045,.026,.046),'nav')
    # Authored tiny safety plates, not artwork pixels: sparse printed bars and
    # a name stencil sit within their panels instead of overwhelming the hull.
    atlas=Image.new('RGBA',(1024,256),(52,58,62,255))
    d=ImageDraw.Draw(atlas)
    try:f=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',53)
    except OSError:f=ImageFont.load_default()
    d.text((24,26),'BASHI BAZOUK',font=f,fill=(207,208,196,255))
    d.text((29,106),'SALVAGE / 05',font=f,fill=(150,157,156,255))
    d.line((28,195,800,195),fill=(146,88,55,255),width=6)
    for j in range(14):d.rectangle((28+j*26,218,42+j*26,224),fill=(150,157,156,255))
    h.materials['markings']=PBRMaterial(name='Bashi / authored name and service stencil',
        baseColorFactor=np.ones(4),baseColorTexture=atlas,metallicFactor=.1,roughnessFactor=.83)
    m=trimesh.Trimesh(vertices=[[-.29,.88,.624],[.21,.88,.624],[.21,1.02,.624],[-.29,1.02,.624]],
                      faces=[[0,1,2],[0,2,3]],process=False)
    m.visual=TextureVisuals(uv=[[0,0],[1,0],[1,1],[0,1]],material=h.materials['markings'])
    h.parts['markings'].append(m)
    return h
