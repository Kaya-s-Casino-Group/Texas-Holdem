from pathlib import Path
source = Path(r'F:\Texas-Holdem\blender_output\render_heavy_attack.py').read_text(encoding='utf-8')
source = source.replace('02_Attack_全身重挥', '02_Attack_抬手蓄力_下劈')
source = source.replace('02_attack_heavy.mp4', '02_attack_downstrike.mp4')
source = source.replace('heavy_attack_%02d.png', 'downstrike_%02d.png')
source = source.replace("rig.animation_data.action =", "camdata.ortho_scale = 1.85\ncam.rotation_euler = (Vector((-0.02,0,.58))-cam.location).to_track_quat('-Z','Y').to_euler()\nrig.animation_data.action =", 1)
exec(compile(source, '<downstrike_render>', 'exec'))
