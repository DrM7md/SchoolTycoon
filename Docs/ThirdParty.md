# الأطراف الثالثة

سجل كل اعتماد يُشحن مع اللعبة أو يلزم لبنائها: النسخة، والرخصة، والأثر على التوزيع دون اتصال. أي اعتماد جديد يُضاف هنا قبل دمجه (انظر `AGENTS.md`). آخر مراجعة: 2026-11-08 (النسخ مأخوذة من csproj في هذا التاريخ وقد تتغير مع أعمال الحفظ الجارية).

## حزم NuGet للنواة

هدف `net10.0` (WPF والاختبارات) بلا حزم خارجية. هدف `netstandard2.1` (Unity) يعتمد على الحزم التالية، وتُنسخ إلى `Unity/SchoolDirectorTycoon/Assets/Plugins/Core` عبر `tools/CopyCoreToUnity.ps1`. المصدر هو ملف `src/SchoolDirectorTycoon.Core/SchoolDirectorTycoon.Core.csproj`؛ عند اختلافه عن هذا الجدول فالملف هو المرجع وحدّث الجدول.

| الحزمة | النسخة | المرجع | الرخصة | دون اتصال |
| --- | --- | --- | --- | --- |
| System.Text.Json | 9.0.11 | مباشر | MIT (Microsoft) | يُشحن DLL مع اللعبة؛ لا اتصال |
| System.Collections.Immutable | 9.0.11 | مباشر | MIT (Microsoft) | يُشحن؛ لا اتصال |
| Microsoft.Bcl.AsyncInterfaces | 9.0.11 | مباشر | MIT (Microsoft) | يُشحن؛ لا اتصال |
| System.Text.Encodings.Web | تُحل عبر System.Text.Json (النسخة الفعلية في `deps.json`) | انتقالي | MIT (Microsoft) | يُشحن؛ لا اتصال |
| System.IO.Pipelines | تُحل عبر System.Text.Json (النسخة الفعلية في `deps.json`) | انتقالي | MIT (Microsoft) | يُشحن؛ لا اتصال |

لا تُنسخ `System.Memory` و`System.Buffers` و`System.Numerics.Vectors` و`System.Threading.Tasks.Extensions` لأنها ضمن `netstandard2.1`، ويُنسخ `System.Runtime.CompilerServices.Unsafe` 6.0.0 (MIT) لأن حزم URP لا تشحنه كتجميعة مستقلة (التفاصيل في [الانتقال إلى Unity](UnityMigration.md)).

ملاحظات قيد التنفيذ: مراجعة نسخ System.Text.Json وتوافقها مع IL2CPP (`link.xml` أو سياق مولَّد) جارية؛ حدّث هذا الجدول بالنسخ النهائية بعد الدمج وبعد أول فتح في Unity.

## حزم Unity

من `Unity/SchoolDirectorTycoon/Packages/manifest.json`. كلها من Unity ضمن Unity Companion License أو رخصة المحرر، وتُخزَّن في ذاكرة الحزم بعد التنزيل الأول فتعمل دون اتصال. النسخ مكتوبة يدويًا وقد يعدّلها مدير الحزم عند الفتح الأول؛ سجّل النسخ الفعلية من `packages-lock.json` بعده.

| الحزمة | النسخة | الاستخدام |
| --- | --- | --- |
| com.unity.render-pipelines.universal | 17.0.4 | URP |
| com.unity.inputsystem | 1.14.2 | الإدخال |
| com.unity.localization | 1.5.4 | جداول النصوص (en ثم ar) |
| com.unity.cinemachine | 3.1.4 | الكاميرا |
| com.unity.test-framework | 1.4.6 | اختبارات EditMode/PlayMode (تطوير فقط) |
| com.unity.ugui | 2.0.0 | uGUI وTextMeshPro |
| com.unity.ide.visualstudio | 2.0.23 | تكامل المحرر (تطوير فقط) |
| com.unity.modules.* (ai, animation, audio, imgui, jsonserialize, physics, ui, uielements) | 1.0.0 | وحدات المحرك المدمجة |

لا حزم Unity Services أو Analytics أو Collab. أي حزمة تتصل بالشبكة ممنوعة في هذه المرحلة.

## أدوات البناء

.NET SDK 10.0.4xx (MIT) وUnity 6 LTS `6000.0.x` (رخصة Unity). لا تُشحن مع اللعبة. انظر [التطوير](Development.md).

## الفن والخطوط (مستقبلًا)

اللعبة بلا صوت (قرار 2026-11-08)، فلا تُضاف أصول صوتية. وحدة `audio` المدمجة في المحرك باقية لاعتماد حزم أخرى عليها، وغير مستخدمة.

لا توجد أصول خارجية حاليًا. لكل أصل يُضاف سطر في الجدول قبل دمجه:

| الأصل | المصدر/المتجر | المؤلف | النسخة أو التاريخ | الرخصة | يسمح بالتوزيع التجاري؟ | يتطلب نسبة؟ | الملفات/المجلد |
| --- | --- | --- | --- | --- | --- | --- | --- |

- احتفظ بنسخة من نص الرخصة أو إيصال الشراء خارج المستودع العام.
- الخطوط العربية: تأكد أن الرخصة تسمح بالتضمين في تطبيق (embedding) لا للويب فقط.
- لا أصول مولَّدة أو مجمّعة دون مصدر ورخصة موثقين.
- أدوات وإضافات المحرر من Asset Store (مثل RTLTMPro إن احتيج إليها) تُسجّل هنا كذلك.
