"""Generate a tileable 128px linear data map: R=crack proximity, G=reveal order.
Run with python3; requires numpy and Pillow. No changes to the source model/UVs.
"""
from pathlib import Path
import numpy as np
from PIL import Image

SIZE = 128
rng = np.random.default_rng(197)
# Periodic jittered cells give irregular branching stone fractures.
seeds = np.array([[(x + rng.uniform(.15, .85)) / 4,
                   (y + rng.uniform(.15, .85)) / 4] for y in range(4) for x in range(4)])
y, x = np.mgrid[:SIZE, :SIZE] / SIZE
# Periodic domain warp keeps the map seamless while bending straight cell edges.
u = x + .018 * np.sin(2*np.pi*(3*y+x)) + .007*np.sin(2*np.pi*(7*y-2*x))
v = y + .018 * np.sin(2*np.pi*(2*x-y)) + .006*np.sin(2*np.pi*(6*x+3*y))
points = np.stack((u, v), axis=-1)
delta = (points[..., None, :] - seeds + .5) % 1 - .5
d2 = np.sum(delta * delta, axis=-1)
nearest = np.argsort(d2, axis=-1)[..., :2]
i, j = nearest[..., 0], nearest[..., 1]
a = np.take_along_axis(delta, i[..., None, None], axis=2)[..., 0, :]
b = np.take_along_axis(delta, j[..., None, None], axis=2)[..., 0, :]
# Distance to the nearest cell boundary, encoded as an 8-texel proximity ramp.
dist = (np.sum(b*b,axis=-1) - np.sum(a*a,axis=-1)) / np.maximum(2*np.linalg.norm(b-a,axis=-1), 1e-6)
proximity = np.clip(1 - dist*SIZE/8, 0, 1)
low, high = np.minimum(i,j), np.maximum(i,j)
edge_order = .10 + .65 * (((low*17 + high*31 + 13) % 97) / 96)
reveal = np.clip(edge_order + .10*np.sin(2*np.pi*(2*u+3*v)), .03, .92)
data = np.stack((proximity, reveal, np.zeros_like(proximity)),axis=-1)
path = Path(__file__).resolve().parents[2] / 'Assets/Art/Models/Creatures/DragonGargoyle/Textures/DragonGargoyle_Cracks.png'
Image.fromarray(np.rint(data*255).astype('uint8')).save(path)
print(f'{path}: {SIZE}x{SIZE}, tileable linear RG data')
