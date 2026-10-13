"""Publish only Unity runtime inputs, preserving meta files and generated assets."""
import hashlib
import json
import os
import shutil
import tempfile
from pathlib import Path
import bpy

DEFAULT_FOLDER = 'Assets/Art/Models/Creatures/DragonGargoyle/VAT'


def preferences():
    addon = bpy.context.preferences.addons.get(__package__)
    return addon.preferences if addon else None


def destination(settings):
    prefs = preferences()
    override = settings.unity_directory
    if override:
        folder = Path(bpy.path.abspath(override)).resolve()
        project = next((p for p in folder.parents if (p/'ProjectSettings/ProjectVersion.txt').is_file()), None)
    else:
        project = Path(bpy.path.abspath(prefs.unity_project)).resolve() if prefs and prefs.unity_project else None
        if project is None and bpy.data.filepath:
            project = next((p for p in Path(bpy.data.filepath).parents
                            if (p/'ProjectSettings/ProjectVersion.txt').is_file()), None)
        if project is None:
            raise RuntimeError('请在插件首选项设置 Unity 项目，或填写场景的 Unity 资源目录。')
        relative = prefs.unity_assets_folder if prefs else DEFAULT_FOLDER
        folder = (project/relative).resolve()
    if project is None or not (project/'ProjectSettings/ProjectVersion.txt').is_file():
        raise RuntimeError('Unity 项目必须包含 ProjectSettings/ProjectVersion.txt。')
    assets = (project/'Assets').resolve()
    if not assets.is_dir() or not folder.is_relative_to(assets) or folder == assets:
        raise RuntimeError('Unity 资源目录必须是项目 Assets 内的子目录。')
    return folder, project


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def publish(source, name, settings):
    source = Path(source).resolve()
    folder, project = destination(settings)
    if folder == source:
        raise RuntimeError('Blender 工作目录和 Unity 资源目录必须分开。')
    manifest_name = name + '_vat.json'
    metadata = json.loads((source/manifest_name).read_text())
    if metadata.get('schema') != 'statue-rigid-vat-v1':
        raise RuntimeError('Unsupported VAT manifest.')
    files = [metadata['files'][key] for key in ('mesh', 'position', 'rotation')]
    expected = [name+'.fbx', name+'_position.exr', name+'_rotation.exr']
    if files != expected:
        raise RuntimeError('VAT file names must match the animation name.')
    report = json.loads((source/metadata['files']['validation']).read_text())
    if not report.get('geometry_nodes_texture_decode_checked') or not report.get('source_cache_still_baked'):
        raise RuntimeError('VAT package has not passed export validation.')
    # Library is outside AssetDatabase scanning but on the project's filesystem.
    staging_root = project/'Library/StatueVATPublish'
    staging_root.mkdir(parents=True, exist_ok=True)
    staging = Path(tempfile.mkdtemp(prefix=name+'_', dir=staging_root))
    changed = []
    try:
        for filename in files:
            shutil.copyfile(source/filename, staging/filename)
        metadata['unity_export'] = {'runtime_files_sha256': {f:digest(staging/f) for f in files}}
        (staging/manifest_name).write_text(json.dumps(metadata, indent=2)+'\n')
        folder.mkdir(parents=True, exist_ok=True)
        # Manifest is the commit marker; publish it last. Never touch .meta,
        # _Unity.asset, _Surface.mat or Blender diagnostic files.
        for filename in files+[manifest_name]:
            target = folder/filename
            if target.is_file() and digest(target) == digest(staging/filename):
                continue
            backup = staging/(filename+'.previous')
            if target.exists():
                shutil.copyfile(target, backup)
            changed.append((target, backup))
            os.replace(staging/filename, target)
    except Exception:
        for target, backup in reversed(changed):
            if backup.exists():
                os.replace(backup, target)
            elif target.exists():
                target.unlink()
        raise
    finally:
        shutil.rmtree(staging)
    return str(folder)
