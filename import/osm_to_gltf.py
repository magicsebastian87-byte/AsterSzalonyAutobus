#!/usr/bin/env python3
import bpy, json, os, sys, math
from mathutils import Vector
import argparse
parser = argparse.ArgumentParser()
parser.add_argument("--bbox", required=True)
parser.add_argument("--output", required=True)
parser.add_argument("--chunk-size", type=int, default=1000)
parser.add_argument("--simplify", type=float, default=0.5)
args = parser.parse_args(sys.argv[sys.argv.index("--")+1:])
min_lon,min_lat,max_lon,max_lat = map(float, args.bbox.split(","))
center_lon = (min_lon+max_lon)/2; center_lat = (min_lat+max_lat)/2
bpy.context.scene.unit_settings.system = 'METRIC'
for obj in list(bpy.data.objects):
    if obj.type in {'MESH','CURVE','CAMERA','LIGHT'}:
        bpy.data.objects.remove(obj, do_unlink=True)
def mat(name, col, rough=0.8, met=0):
    m=bpy.data.materials.new(name); m.use_nodes=True
    m.node_tree.nodes["Principled BSDF"].inputs['Base Color'].default_value=(*col,1)
    m.node_tree.nodes["Principled BSDF"].inputs['Roughness'].default_value=rough
    m.node_tree.nodes["Principled BSDF"].inputs['Metallic'].default_value=met
    return m
mat_road=mat("Road",(0.15,0.15,0.18),0.9)
mat_building=mat("Building",(0.4,0.35,0.3),0.7)
mat_ground=mat("Ground",(0.2,0.25,0.15),1.0)
mat_sidewalk=mat("Sidewalk",(0.35,0.35,0.35),0.8)
mat_grass=mat("Grass",(0.15,0.3,0.1),1.0)
print(f"[OSM] BBox: {min_lon},{min_lat} - {max_lon},{max_lat}")
print(f"[OSM] Chunk: {args.chunk_size}m")
try:
    if "blosm" not in dir(bpy.types): raise Exception("No Blosm")
    bpy.ops.blosm.import_osm(bbox=(min_lon,min_lat,max_lon,max_lat),use_cache=True,import_buildings=True,import_roads=True,import_landuse=True,import_natural=True,simplify_factor=args.simplify)
    for o in bpy.context.scene.objects:
        if o.type!='MESH': continue
        n=o.name.lower(); o["godot_collision"]="static"
        if "highway" in n or "road" in n: o["godot_type"]="road"; o.active_material=mat_road
        elif "building" in n: o["godot_type"]="building"; o.active_material=mat_building
        elif "grass" in n or "forest" in n or "park" in n: o.active_material=mat_grass
        elif "sidewalk" in n or "footway" in n: o.active_material=mat_sidewalk
        else: o["godot_type"]="ground"; o.active_material=mat_ground
except Exception as e:
    print(f"[Fallback] {e}")
    bpy.ops.mesh.primitive_plane_add(size=20000,location=(0,0,0))
    g=bpy.context.active_object; g.name="Ground"; g.active_material=mat_ground; g["godot_type"]="ground"; g["godot_collision"]="static"
    pts=[(0,0),(50,-500),(200,-2000),(300,-4000),(500,-7000),(600,-10000)]
    cd=bpy.data.curves.new("Road","CURVE"); cd.dimensions='3D'; cd.bevel_depth=6; cd.bevel_resolution=4; cd.fill_mode='FULL'
    sp=cd.splines.new('BEZIER'); sp.bezier_points.add(len(pts)-1)
    for i,(x,z) in enumerate(pts):
        bp=sp.bezier_points[i]; bp.co=(x,0.05,z); bp.handle_left_type='AUTO'; bp.handle_right_type='AUTO'
    co=bpy.data.objects.new("DK91",cd); bpy.context.collection.objects.link(co)
    bpy.context.view_layer.objects.active=co; bpy.ops.object.convert(target='MESH')
    r=bpy.context.active_object; r.active_material=mat_road; r["godot_type"]="road"; r["godot_collision"]="static"
    for x,z,name in [(0,0,"Ozorkow"),(-20,-500,"Wyszyńskiego"),(180,-2000,"Zgierz"),(280,-4000,"Pabianice"),(550,-7000,"Retkinia"),(580,-9500,"Manufaktura")]:
        bpy.ops.mesh.primitive_plane_add(size=1,location=(x+5,0.07,z))
        s=bpy.context.active_object; s.name=f"BusStop_{name}"; s.scale=(6,1,12); bpy.ops.object.transform_apply(scale=True)
        s.active_material=mat_sidewalk; s["godot_type"]="bus_stop"; s["godot_collision"]="trigger"
        bpy.ops.object.empty_add(type='ARROWS',location=(x+5,0.5,z-6))
        m=bpy.context.active_object; m.name=f"Marker_{name}"; m["godot_type"]="stop_marker"
        bpy.ops.object.text_add(location=(x+5,4,z))
        t=bpy.context.active_object; t.name=f"Label_{name}"; t.data.body=name; t.data.size=1.5; t.rotation_euler=(1.57,0,0)
    import random; random.seed(42)
    for i in range(len(pts)-1):
        x1,z1=pts[i]; x2,z2=pts[i+1]; dx,dz=x2-x1,z2-z1
        L=math.hypot(dx,dz); if L==0: continue
        ux,uz=dx/L,dz/L; nx,nz=-uz,ux
        for side in [-1,1]:
            for j in range(int(L/80)+1):
                t=j/max(1,int(L/80)); bx=x1+ux*L*t+nx*side*random.uniform(15,40); bz=z1+uz*L*t+nz*side*random.uniform(15,40)
                w,d,h=random.uniform(8,20),random.uniform(10,30),random.uniform(6,25)
                bpy.ops.mesh.primitive_cube_add(size=1,location=(bx,h/2,bz))
                b=bpy.context.active_object; b.name=f"Bldg_{i}_{side}_{j}"; b.scale=(w/2,h/2,d/2); bpy.ops.object.transform_apply(scale=True)
                b.active_material=mat_building; b["godot_type"]="building"; b["godot_collision"]="static"
out=os.path.dirname(args.output); os.makedirs(out,exist_ok=True)
chunks={}
for o in bpy.context.scene.objects:
    if o.type!='MESH': continue
    c=sum((o.matrix_world @ Vector(v) for v in o.bound_box),Vector())/8
    cx=int(c.x//args.chunk_size)*args.chunk_size; cz=int(c.z//args.chunk_size)*args.chunk_size
    k=f"{cx}_{cz}"; chunks.setdefault(k,[]).append(o)
for k,objs in chunks.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(out,f"World_{k}.gltf"),export_format='GLTF_SEPARATE',use_selection=True,export_apply=True,export_yup=True,export_extras=True)
    print(f"[Export] {k}: {len(objs)} objs")
with open(os.path.join(out,"world_manifest.json"),'w') as f:
    json.dump({"chunks":list(chunks.keys()),"chunk_size":args.chunk_size,"center":[center_lon,center_lat],"bbox":[min_lon,min_lat,max_lon,max_lat]},f,indent=2)
print("[OSM Import] Done!")
