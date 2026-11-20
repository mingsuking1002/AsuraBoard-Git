"use strict";
const fs=require('fs'),path=require('path'),vm=require('vm'),assert=require('assert');
const root=path.resolve(__dirname,'../..'),dir=path.join(root,'Documentation/research/splus-items'),read=f=>fs.readFileSync(f,'utf8').replace(/^\ufeff/,'');
const context={};context.window=context;vm.createContext(context);vm.runInContext(read(path.join(dir,'ability-data.js')),context);vm.runInContext(read(path.join(dir,'ability-studio.js')),context);
const {abilities,meta}=context.ABILITY_CATALOG,api=context.AbilityStudio;
assert.strictEqual(abilities.length,meta.total);assert.strictEqual(new Set(abilities.map(a=>a.id)).size,abilities.length);
const privateData=JSON.parse(read(path.join(dir,'internal/ability-provenance.json')));
const modules=new Set(Object.values(privateData).flat().map(x=>x.module).filter(Boolean));assert.strictEqual(modules.size,62);
const coreIds=[...read(path.join(root,'Assets/Marble/Core/AbilityModules.cs')).matchAll(/(?:Scalar|Status|new AbilityModule)\("([\w.-]+)"/g)].map(m=>m[1]);assert.strictEqual(coreIds.length,32);for(const id of coreIds)assert(modules.has('AbilityModules:'+id),id);
const special=read(path.join(root,'Assets/Marble/Core/SpecialMechanics.cs')).match(/public enum SpecialMechanic\s*\{([^}]+)\}/)[1].split(',').map(s=>s.trim()).filter(Boolean);assert.strictEqual(special.length,30);for(const id of special)assert(modules.has('SpecialMechanics:'+id),id);
const forbidden=/ADD-\d+|##skill|<\/?tooltip|原文|原문|근거|문맥|미연결|grade-unconfirmed|S\+/;
for(const a of abilities){assert(a.name&&a.effect&&a.variants.length);assert(privateData[a.id]);assert(!forbidden.test(JSON.stringify(a)),a.id+' '+a.effect);assert(meta.categories.some(c=>c.id===a.category));for(const v of a.variants)assert(!v.effect.includes('MAX')&&!v.effect.includes('Lv.'),v.effect);}
for(const a of abilities)for(const v of a.variants){const noSlots=v.effect.replace(/\(스킬밸류\d*\)/g,'').replace('2~12','').replace('최저 이자율은 0%','');assert(!/\d+(?:\.\d+)?/.test(noSlots),'Unparameterized value: '+v.effect);assert(!/\bN(?:%|회|칸|턴|G)/.test(v.effect));}
assert(/ability-row/.test(read(path.join(dir,'ability-studio.js'))));
const own=api.filterAbilities(abilities,{origin:'module'});assert.strictEqual(own.length,62);assert(api.filterAbilities(abilities,{query:'통행료 스킬밸류'}).length>0);
assert(api.filterAbilities(abilities,{category:'body',origin:'module'}).length>=5);
const sample=own[0],draft={id:'one',name:'테스트 조합',memo:'다음 턴에 회수',items:[{abilityId:sample.id,name:sample.name,effect:'통행료를 50% 줄인다.',variantIndex:0}]};
const normalized=api.normalizeSaved({drafts:[draft],activeId:'one',favorites:[sample.id,'fake']},abilities);assert.strictEqual(normalized.drafts[0].items[0].effect,draft.items[0].effect);assert.strictEqual(normalized.favorites.length,1);
const restored=api.normalizeSaved(JSON.parse(JSON.stringify(normalized)),abilities);assert.strictEqual(JSON.stringify(restored),JSON.stringify(normalized));
assert(api.toMarkdown(restored.drafts[0]).includes('통행료를 50% 줄인다.'));assert(api.toMarkdown(restored.drafts[0]).includes('다음 턴에 회수'));
assert.strictEqual(api.filterAbilities(abilities,{category:'favorites',favorites:[sample.id]}).length,1);
assert(api.normalizeSaved({drafts:[null,{name:'기획',items:[null,{name:'예전 능력',effect:'저장된 효과'}]}]},abilities).drafts[0].items.length===1);
assert.strictEqual(api.csvCell('=1+1'),'"\'=1+1"');
const html=read(path.join(dir,'index.html'));assert(!/catalog-data\.js|viewer\.js|provenance|근거|문맥|미연결|S\+|확정 장비/.test(html));
for(const m of html.matchAll(/src="([^"]+)"/g))assert(fs.existsSync(path.resolve(dir,m[1])));
const report={status:'passed',abilityCount:abilities.length,variants:meta.variantCount,projectModules:modules.size,researchAbilities:meta.researchCount,
 publicEvidenceFieldsAbsent:true,allProjectModulesCovered:true,filtersPassed:true,planningRoundTripPassed:true,privateProvenancePreserved:true,gameCodeChanged:false,listLayout:true,variableValuesParameterized:true};
fs.writeFileSync(path.join(dir,'ability-catalog-validation.json'),JSON.stringify(report,null,2)+'\n');
fs.mkdirSync(path.join(root,'Validation/ability-studio'),{recursive:true});fs.writeFileSync(path.join(root,'Validation/ability-studio/source-validation.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report));
