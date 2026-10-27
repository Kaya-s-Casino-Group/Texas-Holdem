import bpy, sys, os, json
from mathutils import Vector

OUT = r'F:\Texas-Holdem\blender_output\demo_videos'
os.makedirs(OUT, exist_ok=True)
s = bpy.context.scene
rig = bpy.data.objects['Monster_Rig']
def enum(obj, prop, value):
    valid = [x.identifier for x in obj.bl_rna.properties[prop].enum_items]
    assert value in valid, (prop, valid)
    setattr(obj, prop, value)
s.render.engine = 'BLENDER_EEVEE_NEXT'
s.eevee.taa_render_samples = 16
s.render.resolution_x = 640
s.render.resolution_y = 480
s.render.resolution_percentage = 100
s.render.fps = 24
s.render.film_transparent = False
camdata = bpy.data.cameras.new('Demo_Camera')
cam = bpy.data.objects.new('Demo_Camera', camdata)
s.collection.objects.link(cam)
cam.location = (-1.65, -3.0, 1.45)
cam.rotation_euler = (Vector((-0.02, 0, .49)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
enum(camdata, 'type', 'ORTHO')
camdata.ortho_scale = 1.6
s.camera = cam
def area(name, loc, energy, size, color):
    valid = [x.identifier for x in bpy.types.Light.bl_rna.properties['type'].enum_items]
    assert 'AREA' in valid
    data = bpy.data.lights.new(name, 'AREA')
    data.energy = energy; data.shape = 'DISK' if 'DISK' in [x.identifier for x in data.bl_rna.properties['shape'].enum_items] else data.shape
    data.size = size; data.color = color
    obj = bpy.data.objects.new(name, data); s.collection.objects.link(obj)
    obj.location = loc
    obj.rotation_euler = (Vector((0,0,.5))-obj.location).to_track_quat('-Z','Y').to_euler()
area('Demo_Key', (-2,-3,4), 350, 3, (1,.92,.84))
area('Demo_Fill', (1,-1,2), 100, 2, (.7,.8,1))
area('Demo_Rim', (1,2,3), 250, 2, (.85,.75,1))
world = bpy.data.worlds.new('Demo_World'); s.world = world; world.use_nodes = True
bg = next(n for n in world.node_tree.nodes if n.type == 'BACKGROUND')
bg.inputs['Color'].default_value = (.055,.065,.085,1)
bg.inputs['Strength'].default_value = .4
clips = [('01_idle', '01_Idle_待机', 72), ('02_attack', '02_Attack_手部挥击', 41), ('03_hit', '03_Hit_受击', 33), ('04_powerup', '04_PowerUp_强化', 73)]
def select(name):
    rig.animation_data.action = bpy.data.actions[name]
    rig.animation_data.action_slot = rig.animation_data.action.slots[0]
    if rig.animation_data.nla_tracks:
        for t in rig.animation_data.nla_tracks: t.mute=True
    s.frame_set(1)
select(clips[0][1])
if '--preview' in sys.argv:
    enum(s.render.image_settings,'file_format','PNG')
    s.render.filepath = os.path.join(OUT,'preview.png')
    bpy.ops.render.render(write_still=True)
else:
    enum(s.render.image_settings, 'file_format', 'FFMPEG')
    enum(s.render.ffmpeg, 'format', 'MPEG4')
    enum(s.render.ffmpeg, 'codec', 'H264')
    enum(s.render.ffmpeg, 'constant_rate_factor', 'NONE')
    enum(s.render.ffmpeg, 'ffmpeg_preset', 'GOOD')
    s.render.ffmpeg.video_bitrate = 600
    s.render.ffmpeg.minrate = 0
    s.render.ffmpeg.maxrate = 800
    s.render.ffmpeg.buffersize = 1024
    results=[]
    for filename,name,end in clips:
        select(name); s.frame_start=1; s.frame_end=end
        s.render.filepath=os.path.join(OUT,filename+'.mp4')
        s.render.use_file_extension=True
        bpy.ops.render.render(animation=True)
        results.append({'clip':filename,'frames':end,'fps':24,'file':s.render.filepath})
        with open(os.path.join(OUT,'completed.json'),'w',encoding='utf-8') as f:json.dump(results,f,indent=2)
    select(clips[0][1]);s.frame_start=1;s.frame_end=72
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'monster_demo_render.blend'))
    print('ALL_FOUR_DEMOS_COMPLETE',flush=True)
