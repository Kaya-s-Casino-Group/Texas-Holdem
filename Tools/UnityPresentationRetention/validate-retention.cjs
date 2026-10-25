// Run from the project root: node Tools/UnityPresentationRetention/validate-retention.cjs
// Read-only checks of Unity assets and the preserved rule workspace; writes only the report.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '../..');
const rel = p => path.relative(root, p).split(path.sep).join('/');
const read = p => fs.readFileSync(path.join(root, p), 'utf8').replace(/^\uFEFF/, '');
const json = p => JSON.parse(read(p));
const exists = p => fs.existsSync(path.join(root, p));
function walk(dir) {
  const absolute = path.join(root, dir);
  if (!fs.existsSync(absolute)) return [];
  return fs.readdirSync(absolute, { withFileTypes: true }).flatMap(entry => {
    const child = `${dir}/${entry.name}`;
    if (entry.isSymbolicLink()) return [];
    return entry.isDirectory() ? walk(child) : [child];
  });
}
const plan = json('Tools/UnityPresentationRetention/cleanup-plan.json');
const inventory = json('Tools/UnityPresentationRetention/inventory.json');
const baseline = json('Tools/UnityPresentationRetention/rules-before.json');
const assetFiles = walk('Assets');
const ruleFiles = walk('External/BattleToolkit/Engine');
const packageFiles = [...walk('Packages'), ...walk('Library/PackageCache')];
const allFiles = [...assetFiles, ...ruleFiles, ...packageFiles];
const guids = new Map();
for (const file of allFiles.filter(p => p.endsWith('.meta'))) {
  const guid = read(file).match(/^guid: ([a-f0-9]{32})/m)?.[1];
  if (!guid) continue;
  const targets = guids.get(guid) || new Set();
  targets.add(file.slice(0, -5));
  guids.set(guid, targets);
}
const missingReferences = [];
const duplicateAssetGuids = [];
for (const [guid, targets] of guids) {
  const assets = [...targets].filter(p => p.startsWith('Assets/'));
  if (assets.length > 1) duplicateAssetGuids.push({ guid, assets });
}
for (const file of [...assetFiles, ...walk('ProjectSettings')]) {
  if (!/\.(meta|asset|mat|prefab|unity|shadergraph|shadersubgraph)$/.test(file)) continue;
  const text = read(file);
  for (const match of text.matchAll(/guid: ([a-f0-9]{32})/g)) {
    if (/^0{16}/.test(match[1])) continue;
    if (!guids.has(match[1])) missingReferences.push({ file, guid: match[1] });
  }
}
const assemblies = new Map();
for (const file of allFiles.filter(p => p.endsWith('.asmdef'))) {
  const def = json(file);
  assemblies.set(def.name, { ...def, path: file });
}
const missingAssemblyReferences = [];
for (const a of assemblies.values()) {
  if (!a.path.startsWith('Assets/')) continue;
  for (const reference of a.references || []) {
    if (reference.startsWith('GUID:')) {
      if (!guids.has(reference.slice(5))) missingAssemblyReferences.push({ assembly: a.name, reference });
    } else if (!assemblies.has(reference)) missingAssemblyReferences.push({ assembly: a.name, reference });
  }
}
const alteredRules = baseline.files.filter(file => {
  const p = path.join(root, 'External/BattleToolkit', file.path);
  return !fs.existsSync(p) || crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex').toUpperCase() !== file.sha256;
}).map(file => file.path);
const missingKeptFiles = inventory.files.filter(file => !exists(file));
const missingAdditionalAssets = plan.keptAssets.filter(file => !exists(file));
const remainingDeletedAssets = plan.deletedAssets.filter(exists);
const missingMeta = assetFiles.filter(file => !file.endsWith('.meta') && !exists(file + '.meta'));
const orphanMeta = assetFiles.filter(file => file.endsWith('.meta') && !exists(file.slice(0, -5)));
const forbiddenAssemblies = [...assemblies.values()].filter(a => a.path.startsWith('Assets/') &&
  /^(CardMatch|CardInteraction|CardPresentation|Poker\.(SingleBattle|Battle2DValidation|DrawBattle|RunFlow|Scoring))\./.test(a.name)).map(a => a.name);
const shaderCount = assetFiles.filter(file => file.endsWith('.shader')).length;
const includeCount = assetFiles.filter(file => /\.(hlsl|cginc)$/.test(file)).length;
const buildScenesEmpty = /m_Scenes: \[\]/.test(read('ProjectSettings/EditorBuildSettings.asset'));
const report = {
  checkedAt: new Date().toISOString(),
  validation: 'Static filesystem, serialized GUID, assembly references and rule-content hashes. Unity tests are reported separately.',
  counts: { assetFiles: assetFiles.length, shaders: shaderCount, includes: includeCount, ruleFilesHashed: baseline.files.length },
  missingKeptFiles, missingAdditionalAssets, remainingDeletedAssets, missingMeta, orphanMeta,
  missingReferences, duplicateAssetGuids, missingAssemblyReferences, forbiddenAssemblies, alteredRules,
  buildScenesEmpty,
};
report.passed = [missingKeptFiles, missingAdditionalAssets, remainingDeletedAssets, missingMeta, orphanMeta,
  missingReferences, duplicateAssetGuids, missingAssemblyReferences, forbiddenAssemblies, alteredRules].every(items => items.length === 0)
  && buildScenesEmpty && shaderCount === 40 && includeCount === 26;
fs.writeFileSync(path.join(__dirname, 'static-validation.json'), JSON.stringify(report, null, 2) + '\n');
console.log(JSON.stringify(report, null, 2));
process.exitCode = report.passed ? 0 : 1;
