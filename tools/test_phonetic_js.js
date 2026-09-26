// Checks the web-page Avro engine (src/Obhijatri.App/Web/avro-phonetic.js) against the reference
// cases in tests/Fixtures/phonetic-cases.json. Run from the repository root:  node tools/test_phonetic_js.js
const fs = require("fs");
const path = require("path");
const root = path.join(__dirname, "..");
const source = fs.readFileSync(path.join(root, "src/Obhijatri.App/Web/avro-phonetic.js"), "utf8");
const rules = JSON.parse(fs.readFileSync(path.join(root, "src/Obhijatri.Bangla/Phonetic/AvroPhoneticRules.json"), "utf8"));
const createAvroPhonetic = new Function(source + "\nreturn createAvroPhonetic;")();
const avro = createAvroPhonetic(rules);
const cases = JSON.parse(fs.readFileSync(path.join(root, "tests/Fixtures/phonetic-cases.json"), "utf8")).cases;
let failed = 0;
for (const c of cases) {
    const got = avro.parse(c.input);
    if (got !== c.expected) {
        failed++;
        console.log(`FAIL ${c.input}: expected ${c.expected}, got ${got}`);
    }
}
console.log(`${cases.length - failed}/${cases.length} passed`);
process.exit(failed ? 1 : 0);
