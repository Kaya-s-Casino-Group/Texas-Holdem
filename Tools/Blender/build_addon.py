"""Build the installable addon ZIP from the single source package."""
from pathlib import Path
import ast
import zipfile

root = Path(__file__).resolve().parent
package = root/'statue_fracture'
output = root/'dist/statue_fracture.zip'
output.parent.mkdir(exist_ok=True)
with zipfile.ZipFile(output, 'w', compression=zipfile.ZIP_DEFLATED) as archive:
    for path in sorted(package.glob('*.py')):
        ast.parse(path.read_text(), filename=str(path))
        archive.write(path, arcname=path.relative_to(root))
print(output)
