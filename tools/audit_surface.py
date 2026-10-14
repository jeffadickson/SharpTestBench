"""Inventory public definitions in pinned cocotb sources; not a parity percentage."""
import ast,csv,sys
from pathlib import Path
source=Path(sys.argv[1]);target=Path(__file__).resolve().parents[1]/'docs/UPSTREAM_API_INVENTORY.csv'
rows=[]
for path in sorted((source/'src/cocotb').rglob('*.py')):
 module='.'.join(path.relative_to(source/'src').with_suffix('').parts)
 try:tree=ast.parse(path.read_text())
 except SyntaxError:continue
 for node in tree.body:
  if isinstance(node,(ast.ClassDef,ast.FunctionDef,ast.AsyncFunctionDef)) and not node.name.startswith('_'):
   rows.append((module,node.name,node.lineno,'review-required'))
   if isinstance(node,ast.ClassDef):
    for child in node.body:
     if isinstance(child,(ast.FunctionDef,ast.AsyncFunctionDef)) and not child.name.startswith('_'):rows.append((module,node.name+'.'+child.name,child.lineno,'review-required'))
with target.open('w')as f:
 w=csv.writer(f,lineterminator="\n");w.writerow(['Module','Definition','SourceLine','ExactSemanticParity']);w.writerows(rows)
print(len(rows),'public definitions inventoried (includes definitions in internal modules; excludes aliases and dynamic members)')
