# التطوير والتحقق والنشر

## الإصدارات

| المكوّن | الوضع |
| --- | --- |
| ‎.NET وWPF | استهداف سلسلة ‎.NET 10 LTS. أحدث Runtime مستقر وفق Microsoft في 7 نوفمبر 2026 هو 10.0.14، ومثبت في بيئة التحقق. |
| SDK | أحدث إصدار مستقر في الفرع 10.0.4xx هو 10.0.403. اختُبر البناء بـ10.0.402 المتاح محليًا بعد تعذّر الوصول إلى مضيف التنزيل من البيئة. يختار `global.json` أحدث تصحيح 10.0.4xx متاح ويقبل 10.0.403 مباشرة. |
| NuGet | هدف `net10.0` بلا حزم خارجية. هدف `netstandard2.1` (Unity) يعتمد على حزم Microsoft برخصة MIT تُشحن مع اللعبة فتبقى تعمل دون اتصال؛ النسخ الدقيقة في [الأطراف الثالثة](ThirdParty.md). مصادر `NuGet.Config` فارغة، فتُستعاد من ذاكرة الحزم المحلية. |

[تنزيلات .NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) · [دورة الدعم](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)

## عميل Unity (قيد التجهيز)

المحرك المعتمد Unity 6 مع URP. المتطلبات والقيود في [الانتقال إلى Unity](UnityMigration.md).

- **المشروع:** `Unity/SchoolDirectorTycoon`. النسخة المستهدفة في `ProjectSettings/ProjectVersion.txt` هي `6000.0.85f2` (Unity 6 LTS). المحرر المثبّت على جهاز التطوير `6000.0.85f2`. **مسار طويل:** مسار المستودع طويل فتفشل بعض ملفات URP بخطأ `DirectoryNotFoundException` رغم تفعيل المسارات الطويلة في Windows؛ افتح المشروع عبر الوصلة القصيرة `D:\sdt` (`New-Item -ItemType Junction -Path D:\sdt -Target <مسار المشروع>`). اختبارات EditMode من سطر الأوامر: `Unity.exe -batchmode -projectPath D:\sdt -runTests -testPlatform EditMode -testResults <ملف>`.
- **الحزم** (`Packages/manifest.json`، كلها من Unity ضمن رخصة Unity Companion أو رخصة المحرر، وتعمل دون اتصال بعد التنزيل الأول): URP 17.0.4، وInput System 1.14.2، وLocalization 1.5.4، وCinemachine 3.1.4، وTest Framework 1.4.6، وuGUI 2.0.0 (يتضمن TextMeshPro في Unity 6)، وVisual Studio Editor 2.0.23. لا حزم Unity Services أو Analytics أو Collab. النسخ مكتوبة يدويًا، فقد يعدّلها مدير الحزم عند الفتح الأول؛ سجّل النسخ الفعلية هنا بعده.
- **نسخ النواة:** شغّل `powershell -ExecutionPolicy Bypass -File tools/CopyCoreToUnity.ps1` بعد أي تغيير في `Core`. يبني هدف `netstandard2.1` وينسخ DLL النواة وتابعياتها من `deps.json` وذاكرة NuGet المحلية إلى `Assets/Plugins/Core`، ويحتفظ بملفات `.meta`. لا تعدّل هذه الملفات داخل Unity.
- **الاختبارات:** Window ثم General ثم Test Runner، تبويب EditMode. أو دون واجهة: `Unity.exe -batchmode -projectPath Unity/SchoolDirectorTycoon -runTests -testPlatform EditMode -testResults results.xml`.

## لغة الوثائق

العربية لغة الوثائق الافتراضية، والمعرّفات البرمجية وأسماء الملفات والأوامر بالإنجليزية كما هي في الكود. الوثائق المكتوبة بالإنجليزية (مثل [خطة الإنتاج](ProductionPlan.md) و[دراسة السوق](MarketResearch.md) و[التعليم في قطر](QatarEducation.md)) تبدأ بسطر «ملخص عربي» ولا تُترجم كاملة. أي وثيقة إنجليزية جديدة تتبع القاعدة نفسها.

## من جذر المشروع على Windows

```powershell
dotnet --version
dotnet build -c Release -m:1 -p:UseSharedCompilation=false
dotnet run --project tests/SchoolDirectorTycoon.Tests -c Release --no-build
dotnet run --project tests/SchoolDirectorTycoon.Tests.NetStandard -c Release --no-build
dotnet run --project tests/SchoolDirectorTycoon.Desktop.Tests -c Release --no-build
dotnet run --project src/SchoolDirectorTycoon.Desktop -c Release --no-build
```

في البيئات التي تقيّد مجلد المستخدم أو الملفات المؤقتة، وجّه المسارات إلى مجلد محلي قابل للكتابة قبل البناء والاختبار.

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD '.local-sdk'
$env:TEMP = $env:TMP = (New-Item -ItemType Directory -Force .local-sdk/test-temp).FullName
```

## نشر Windows x64

نفّذ باستخدام SDK بمعمارية x64. انسخ مجلد النشر كاملًا عند مشاركة اللعبة. الحزمة تعتمد على Desktop Runtime المثبت لدى اللاعب. بعد تثبيته تعمل اللعبة دون شبكة.

```powershell
dotnet publish src/SchoolDirectorTycoon.Desktop -c Release -m:1 -p:UseSharedCompilation=false --self-contained false -o artifacts/SchoolDirectorTycoon-Windows
Compress-Archive -Path artifacts/SchoolDirectorTycoon-Windows/* -DestinationPath artifacts/SchoolDirectorTycoon-Windows-x64.zip -Force
```

## بوابة الإصدار

- شغّل الاختبارين. اختبار النواة يمر على 128 حملة باستراتيجيتين على 64 بذرة، ويغطي الخسارة والمحاسبة وحفظ الحالة ورفض الملفات التالفة واختبار ترحيل الحفظ القديم إلى شبكة الحرم.
- يتحقق اختبار WPF من اتجاه اللغتين ونص الأزرار والتقارير واستمرار الحملة والحفظ التلقائي. يتحقق أيضًا من دليل البداية وتنبيه السعة وحفظ اللغة والرجوع للعربية عند تلف ملف التفضيل، وتحويل إحداثيات الخريطة وبناء مرفق في موقع يحدده اللاعب.
- في مراجعة الإصدار المرئية جرّب التكبير 125% و150%، التنقل بلوحة المفاتيح، تصغير النافذة، الحفظ والتحميل من الواجهة، وإغلاق اللعبة وإعادة فتحها مع قطع الاتصال. أعد هذه المراجعة لكل لغة.
- حدّث سجل التغييرات والأرقام في وثيقة الأداء بعد القياس، وسجّل إصدار SDK وRuntime المستخدمين.
