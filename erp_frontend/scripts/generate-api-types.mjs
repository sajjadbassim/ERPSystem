// المولِّد الوحيد لأنواع TypeScript من عقد الـ API (R-API-04).
//
// يستدعيه طرفان: `npm run generate:api-types` بلا وسيط فيكتب الملف المتعقَّب،
// والحارس `M04` بوسيط مسار مؤقت فيقارن ولا يكتب في المتعقَّب.
//
// **ولهذا وُجد هذا الملف أصلاً.** لو كرّر كلٌّ منهما سطر الأوامر بنفسه لصار مولِّدان
// ينحرفان بصمت: خيار يُضاف هنا ولا يُضاف هناك، فيقارن الحارس ملفاً وُلِّد بخيارات
// غير التي يُولَّد بها المعتمَد — فيسقط بلا انحراف، أو ينجح مع انحراف حقيقي.
// نفس منطق `OpenApiDocumentProducer` و `ContractPaths` على الجانب الآخر.
//
// والمسارات تُحسب من موقع هذا الملف لا من مجلد العمل: المستدعيان يعملان من مجلدين
// مختلفين، والاعتماد على مجلد العمل كان سيجعل السلوك يختلف بينهما.

import { spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const frontendRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

const contractPath = path.resolve(frontendRoot, "../erp_backend/ErpApi/openapi.json");
const defaultOutputPath = path.resolve(frontendRoot, "api-types/schema.d.ts");
const cliPath = path.resolve(frontendRoot, "node_modules/openapi-typescript/bin/cli.js");

const outputPath = process.argv[2] ? path.resolve(process.argv[2]) : defaultOutputPath;

if (!existsSync(cliPath)) {
  console.error(`تعذّر العثور على openapi-typescript في ${cliPath}. شغّل npm ci داخل erp_frontend.`);
  process.exit(2);
}

if (!existsSync(contractPath)) {
  console.error(`تعذّر العثور على عقد الـ API في ${contractPath}. شغّل ErpApi.ContractTool لإخراجه.`);
  process.exit(2);
}

// الثنائيّ المحلي يُستدعى بـ process.execPath لا بالاسم: النسخة المثبَّتة في
// node_modules هي المعتمدة، ونسخة عامة مختلفة على الجهاز كانت ستُخرج ملفاً آخر
const result = spawnSync(process.execPath, [cliPath, contractPath, "--output", outputPath], {
  stdio: "inherit"
});

process.exit(result.status ?? 1);
