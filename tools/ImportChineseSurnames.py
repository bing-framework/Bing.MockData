"""Extract the fixed MIT Faker zh_CN surnames without executing source code."""
import ast
import hashlib
import json
from pathlib import Path

root=Path(__file__).resolve().parents[1]
source=root/"tools/data/faker-zh-names"
manifest=json.loads((source/"manifest.json").read_text(encoding="utf-8"))
raw=(source/"zh_CN.py").read_bytes()
assert hashlib.sha256(raw).hexdigest()==manifest["sourceSha256"]
tree=ast.parse(raw.decode("utf-8"))
value=next(n.value for n in ast.walk(tree) if isinstance(n,ast.Assign) and any(isinstance(v,ast.Name) and v.id=="last_names" for v in n.targets))
assert isinstance(value,ast.Call) and isinstance(value.func,ast.Name) and value.func.id=="OrderedDict"
names=[entry[0] for entry in ast.literal_eval(value.args[0])]
assert len(names)==manifest["records"]
raw_count=len(names)
names=list(dict.fromkeys(names)) # OrderedDict 同名键保留首次位置；权重不参与均匀抽样。
assert all(isinstance(n,str) and n.strip()==n and n and "\n" not in n for n in names)
output=root/"src/Bing.MockData/Datas/Resources/ChineseLastNames.txt"
output.write_text("\n".join(names)+"\n",encoding="utf-8")
evidence=root/"artifacts/catalog-closure/resources/chinese-surnames.json"
evidence.parent.mkdir(parents=True,exist_ok=True)
evidence.write_text(json.dumps(dict(status="PASS",source=manifest,output=str(output.relative_to(root)),records=len(names),excludedDuplicateKeys=raw_count-len(names),sha256=hashlib.sha256(output.read_bytes()).hexdigest()),ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
print("PASS:",len(names),"unique surnames")
