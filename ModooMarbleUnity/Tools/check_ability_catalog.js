// Headless functional check of the generated offline catalog with a minimal DOM.
// This checks query modes, links and unresolved badges; it is not a visual browser test.
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.resolve(__dirname, '../Documentation/abilities/apk-catalog');
class Element {
  constructor(tag) { this.tag = tag; this.children = []; this.textContent = ''; this.value = ''; this.listeners = {}; }
  append(...children) { this.children.push(...children); }
  replaceChildren(...children) { this.children = children; }
  addEventListener(event, listener) { this.listeners[event] = listener; }
}
const elements = new Map();
for (const id of ['search', 'category', 'scope', 'summary', 'results', 'more', 'bubble', 'bind', 'blackhole', 'itemmode', 'reset']) elements.set(id, new Element(id));
elements.get('scope').value = 'effects';
const context = vm.createContext({
  window: {}, location: { hash: '' }, console,
  setTimeout: () => 1, clearTimeout: () => {},
  document: { getElementById: id => elements.get(id), createElement: tag => new Element(tag), createDocumentFragment: () => new Element('fragment') }
});
const html = fs.readFileSync(path.join(root, 'index.html'), 'utf8');
const inline = html.split('<script>')[1].split('</script>')[0];
new vm.Script(inline, { filename: 'catalog-inline.js' });
vm.runInContext(fs.readFileSync(path.join(root, 'catalog-data.js'), 'utf8'), context, { filename: 'catalog-data.js' });
vm.runInContext(inline, context, { filename: 'catalog-inline.js' });
const tests = [];
function check(name, expression) {
  if (!vm.runInContext(expression, context)) throw new Error(name + ': failed');
  tests.push(name);
}
check('default nonpromotion effect candidates', 'matched.length===10721 && shown===100');
vm.runInContext("$('scope').value='all';$('search').value='';search()", context);
check('all original records remain available', 'matched.length===114847');
vm.runInContext("$('search').value='주사위 정확도';search()", context);
check('AND text search', "matched.length>0 && matched.every(r=>textHays.get(r.id).includes('주사위') && textHays.get(r.id).includes('정확도'))");
vm.runInContext("$('scope').value='items';$('search').value='';search()", context);
check('all ItemList rows available', 'matched.length===29746 && shown===100');
vm.runInContext("$('search').value=items.find(i=>i.names.length).names[0];search()", context);
check('item names are searchable', 'matched.length>0 && matched.some(i=>i.names.length>0)');
context.elementText = function elementText(e) { return e.textContent + e.children.map(c => typeof c === 'string' ? c : elementText(c)).join(' '); };
check('missing name badges and raw keys', "(()=>{const i=items.find(i=>!i.nameResolved);const e=drawItem(i);return elementText(e).includes('이름 키 미해석')&&elementText(e).includes(i.nameKey)})()");
check('missing description badges and raw keys', "(()=>{const i=items.find(i=>!i.descResolved);const e=drawItem(i);return elementText(e).includes('설명 키 미해석')&&elementText(e).includes(i.descKey)})()");
check('description references show actual linked names', "(()=>{const i=items.find(i=>i.numericModifierCandidate&&i.names.length&&i.descRecords.length);const r=recordMap.get(i.descRecords[0]);const e=drawText(r);return r.descRefs.includes(i.id)&&elementText(e).includes(itemName(i))&&elementText(e).includes('능력 소유 연결 아님')})()");
vm.runInContext("$('bubble').listeners.click()", context);
check('bubble shortcut selects effect category', "$('scope').value==='effects'&&$('category').value==='bubble'&&matched.length>0&&matched.every(r=>r.tags.includes('bubble'))");
vm.runInContext("$('itemmode').listeners.click()", context);
check('item shortcut resets query and filters', "$('scope').value==='items'&&$('category').value===''&&$('search').value===''&&matched.length===29746");
const manifest = JSON.parse(fs.readFileSync(path.join(root, 'manifest.json'), 'utf8'));
check('duplicate source keys preserve distinct original records', '(()=>{const seen=new Set();for(const r of records){if(seen.has(r.key))return true;seen.add(r.key)}return false})()');
const report = { status: 'passed', mode: 'Node minimal DOM functional validation; no visual browser claim', casesPassed: tests.length, cases: tests,
  allTextRecords: manifest.coverage.allTextRecords, allItemRows: manifest.coverage.itemRows,
  linkedDescriptionKeysWithBroadEffectCandidate: manifest.coverage.linkedDescriptionKeysWithBroadEffectCandidate,
  linkedDescriptionKeysWithNumericModifierCandidate: manifest.coverage.linkedDescriptionKeysWithNumericModifierCandidate,
  linkedItemDescriptionsWithSkillValuePlaceholder: manifest.coverage.linkedItemDescriptionsWithSkillValuePlaceholder };
fs.writeFileSync(path.join(root, 'ui-validation.json'), JSON.stringify(report, null, 2) + '\n');
console.log(JSON.stringify(report));
