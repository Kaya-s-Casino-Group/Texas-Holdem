from pathlib import Path
setup = Path(r'F:\Texas-Holdem\blender_output\render_demos.py').read_text(encoding='utf-8').split('clips = ')[0]
exec(compile(setup, '<demo_setup>', 'exec'))
rig.animation_data.action = bpy.data.actions['02_Attack_全身重挥']
rig.animation_data.action_slot = rig.animation_data.action.slots[0]
for track in rig.animation_data.nla_tracks:
    track.mute = True
enum(s.render.image_settings, 'file_format', 'PNG')
for frame in (17,23,27):
    s.frame_set(frame)
    s.render.filepath = os.path.join(OUT, 'heavy_attack_%02d.png' % frame)
    bpy.ops.render.render(write_still=True)
enum(s.render.image_settings, 'file_format', 'FFMPEG')
enum(s.render.ffmpeg, 'format', 'MPEG4')
enum(s.render.ffmpeg, 'codec', 'H264')
enum(s.render.ffmpeg, 'constant_rate_factor', 'NONE')
enum(s.render.ffmpeg, 'ffmpeg_preset', 'GOOD')
s.render.ffmpeg.video_bitrate = 600
s.render.ffmpeg.minrate = 0
s.render.ffmpeg.maxrate = 800
s.render.ffmpeg.buffersize = 1024
s.frame_start = 1
s.frame_end = 53
s.frame_set(1)
s.render.filepath = os.path.join(OUT, '02_attack_heavy.mp4')
bpy.ops.render.render(animation=True)
print('HEAVY_ATTACK_RENDER_COMPLETE', flush=True)
