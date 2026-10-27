from pathlib import Path
source = Path(r'F:\Texas-Holdem\blender_output\render_heavy_attack.py').read_text(encoding='utf-8')
source = source.replace('02_Attack_全身重挥', '02_Attack_左手重挥_布料跟随')
source = source.replace('02_attack_heavy.mp4', '02_attack_left_cloth.mp4')
source = source.replace('heavy_attack_%02d.png', 'left_cloth_%02d.png')
exec(compile(source, '<left_cloth_render>', 'exec'))
