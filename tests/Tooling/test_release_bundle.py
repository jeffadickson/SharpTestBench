import hashlib, importlib.util, json, tempfile, unittest, zipfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('release_bundle',ROOT/'tests/Tooling/managed_compat/release_bundle.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)

class ReleaseBundleTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name);self.artifacts=self.root/'artifacts';self.output=self.root/'release'
        for rid in module.RIDS:
            folder=self.artifacts/f'package-evidence-{rid}';folder.mkdir(parents=True)
            feed=self.artifacts/f'packages-{rid}';feed.mkdir()
            checksums={}
            for name in ['SharpTestBench','SharpTestBench.Generators','SharpTestBench.Native.'+rid]:
                package=feed/(name+'.0.8.0-alpha.nupkg')
                with zipfile.ZipFile(package,'w') as z:z.writestr(name+'.nuspec',f'<package><metadata><id>{name}</id><version>0.8.0-alpha</version></metadata></package>')
                checksums[package.name]=hashlib.sha256(package.read_bytes()).hexdigest()
            (folder/'packages.json').write_text(json.dumps(dict(rid=rid,version='0.8.0-alpha',source_revision='abc',source_dirty=False,sha256=checksums)))
            (folder/'manifest.json').write_text(json.dumps(dict(package_version='0.8.0-alpha',source_revision='abc',source_dirty=False)))
            (folder/'results.json').write_text(json.dumps([dict(Name='ResetAndCount',Status='passed')]))
            (folder/'negative-checks.json').write_text(json.dumps(['rid','checksum']))
    def assemble(self):return module.assemble(self.artifacts,self.output,'abc')
    def test_five_packages_and_hashes(self):
        result=self.assemble();self.assertEqual(5,len(result['packages']))
        for p in result['packages']:self.assertEqual(p['sha256'],hashlib.sha256((self.output/p['file']).read_bytes()).hexdigest())
    def test_reject_corruption(self):
        next(self.artifacts.rglob('*.nupkg')).write_bytes(b'corrupted')
        with self.assertRaisesRegex(ValueError,'checksum'):self.assemble()
    def test_reject_missing_platform(self):
        (self.artifacts/'package-evidence-win-x64/packages.json').unlink()
        with self.assertRaisesRegex(ValueError,'evidence'):self.assemble()
    def test_reject_dirty_source(self):
        p=self.artifacts/'package-evidence-linux-x64/packages.json';j=json.loads(p.read_text());j['source_dirty']=True;p.write_text(json.dumps(j))
        with self.assertRaisesRegex(ValueError,'source revision'):self.assemble()
    def test_reject_wrong_source(self):
        with self.assertRaisesRegex(ValueError,'source revision'):module.assemble(self.artifacts,self.output,'other')
    def test_reject_wrong_consumer(self):
        (self.artifacts/'package-evidence-linux-x64/results.json').write_text(json.dumps([dict(Name='Unrelated',Status='passed')]))
        with self.assertRaisesRegex(ValueError,'expected test'):self.assemble()
    def test_reject_missing_negative_evidence(self):
        (self.artifacts/'package-evidence-linux-x64/negative-checks.json').write_text('[]')
        with self.assertRaisesRegex(ValueError,'negative checks'):self.assemble()
