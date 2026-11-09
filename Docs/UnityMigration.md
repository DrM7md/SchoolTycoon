# الانتقال إلى Unity

قرار 2026-11-07: يُبنى العميل القادم للعبة على **Unity 6** بهدف تضاريس ومبانٍ وواجهة بجودة بصرية عالية. تبقى قواعد اللعب في `SchoolDirectorTycoon.Core`، ويصبح Unity طبقة العرض والإدخال والوقت الحقيقي، كما يفعل `Desktop` حاليًا. تبقى نسخة WPF مرجعًا قابلًا للتشغيل حتى يصل عميل Unity إلى مستوى ميزاتها.

## المبادئ

- **النواة مصدر الحقيقة.** لا تُكتب قاعدة لعب داخل MonoBehaviour. كل قرار يمر عبر أوامر `Simulation` ويعيد حالة جديدة، ويرسم Unity تلك الحالة. يبقى ثبات النتائج من نفس الحالة ونفس القرارات شرطًا.
- **المشهد تمثيل وليس حالة.** مواقع المباني على الشبكة تأتي من `CampusLayout`. حركة الطلاب والمؤثرات تجميلية ولا تؤثر على الحسابات.
- **ملفات الحفظ مشتركة.** يقرأ عميل Unity ملفات المخطط الحالي ويرحّل الإصدارات السابقة عبر `SaveStore` نفسه. أي تغيير على الشبكة أو الإحداثيات يحتاج إصدار مخطط جديدًا وترحيلًا واختبارًا.
- **لا اتصال بالشبكة.** لا تحليلات ولا خدمات Unity سحابية في هذه المرحلة. عطّل Unity Analytics وأي حزمة تتطلب اتصالًا.

## اللغة: الإنجليزية أولًا، والتعريب لاحقًا

يُطوَّر عميل Unity بالإنجليزية أولًا، مع تجهيز البنية للعربية من اليوم الأول حتى لا يحتاج التعريب لإعادة كتابة:

- لا نص ثابت داخل المشاهد أو الكود. كل نص يمر عبر مفتاح في جدول نصوص (حزمة Unity Localization)، والجدول الإنجليزي هو المرجع.
- النصوص المركّبة (تقارير، أحداث، أرقام، عملة، تواريخ) تُبنى من قوالب بمعاملات مسماة، وليس بلصق أجزاء الجملة. ترتيب الكلمات يختلف في العربية.
- الواجهة تُبنى بتخطيطات مرنة (Layout Groups وanchors) قابلة للانعكاس أفقيًا. تجنّب المواضع المطلقة التي تفترض القراءة من اليسار.
- اترك مساحة لطول النص وارتفاع الخط العربي، واختر خطًا يدعم الحروف العربية أو خطًا احتياطيًا لها.
- عند التعريب: أضف RTLTMPro (رخصة MIT) أو بديلًا مكافئًا لتشكيل الحروف واتجاه الكتابة في TextMeshPro، ووثّق نسخته ورخصته، واختبر الاتجاهين.
- تبقى ترجمات النواة الحالية للأحداث والتقارير مصدرًا لجدول العربية لاحقًا.

## التوافق التقني

| البند | الوضع |
| --- | --- |
| إطار النواة | Unity 6 يعمل على Mono ويقبل `netstandard2.1`. يُضاف `netstandard2.1` إلى أهداف `Core` بجانب `net10.0` (multi-targeting) حتى يستمر عميل WPF والاختبارات. |
| لغة C# | يُضبط `LangVersion` للنواة على 9 عند بناء `netstandard2.1`. استبدل الميزات الأحدث (مثل `required` وprimary constructors للأصناف وcollection expressions) أو وفّر بدائل. |
| JSON | تحقق من مكتبة التسلسل في `SaveStore`. إن كانت `System.Text.Json` فتحتاج نسخة متوافقة مع `netstandard2.1` داخل Unity، أو طبقة تسلسل بديلة. وثّق القرار ونسخته ورخصته. |
| الإدراج في Unity | تُبنى النواة كـ DLL وتُنسخ إلى `Assets/Plugins/Core`، أو تُربط كمصدر عبر asmdef. يفضّل DLL حتى لا يعدّل أحد القواعد داخل محرر Unity. |
| خط العرض | URP لأداء جيد على Windows ولإمكانية الجوال لاحقًا. |
| الرخصة | Unity Personal ضمن حدود الإيراد التي تعلنها Unity. سجّل نسخة المحرر الدقيقة في وثيقة التطوير عند إنشاء المشروع. |

## الحالة

- المرحلة 1 مكتملة (2026-11-07): `Core` يُبنى لـ `net10.0;netstandard2.1` مع `LangVersion` 9، ورسائل الرفض تحمل `RuleCode`. ملف `bin/Release/netstandard2.1/SchoolDirectorTycoon.Core.dll` هو ما يُنسخ إلى Unity مع حزمه.
- هيكل مشروع Unity جاهز (2026-11-07) في `Unity/SchoolDirectorTycoon`، كُتب يدويًا ثم فُتح في Unity 6 LTS (`6000.0.85f2`) يوم 2026-11-08، وتُرجم دون أخطاء ونجحت اختبارات EditMode الثمانية (Bridge والثبات والحفظ في الخلفية) على Mono داخل المحرر. فيه asmdefs حسب [خطة الإنتاج](ProductionPlan.md): `SDT.Bridge` وحده يرجع إلى DLL النواة، و`SDT.World` و`SDT.UI` يرجعان إلى Bridge فقط. ملفات النواة في `Assets/Plugins/Core` مضبوطة على `isExplicitlyReferenced` حتى لا تصل إليها التجميعات الأخرى تلقائيًا.
- طبقة Bridge الأولى: `GameSession` (تبديل الحالة عبر `Dispatch` مع `StateChanged` و`CommandRejected(RuleCode)`)، و`DayClock` (السرعات 0/1/2/4 ويتوقف عند حدث معلّق أو نهاية الحملة)، و`SaveService` (يغلّف `SaveStore` في `Application.persistentDataPath/Saves` ويحفظ تلقائيًا بعد كل يوم). اختبار EditMode للثبات: نفس البذرة ونفس الأوامر تعطي حالة متطابقة. تُرجم هذا الكود وشُغّل الاختبار على .NET 10 مع بدائل لـ UnityEngine وNUnit فقط، وليس على Mono داخل Unity.
- قرار الحزم التابعة: يُنسخ `System.Text.Json` و`System.Collections.Immutable` و`System.Text.Encodings.Web` و`System.IO.Pipelines` و`Microsoft.Bcl.AsyncInterfaces`. لا يُنسخ `System.Memory` و`System.Buffers` و`System.Numerics.Vectors` و`System.Threading.Tasks.Extensions` لأنها جزء من `netstandard2.1`، ويُنسخ `System.Runtime.CompilerServices.Unsafe` أيضًا: الافتراض الأول أن `com.unity.collections` تشحنه كان خاطئًا، إذ فشل تحميل `System.Collections.Immutable` و`System.Text.Json` دونه عند أول فتح. إن أضافت حزمة لاحقة نسخة منه فأضفه إلى `$skip` في سكربت النسخ.
- مشهد الحرم الأولي (2026-11-08): `Assets/_Project/Scenes/Campus.unity` يُولَّد بالكود من القائمة `SDT/Rebuild Campus Scene` (أو `-executeMethod SchoolDirectorTycoon.EditorTools.CampusSceneBuilder.BuildFromCommandLine`) مع المواد ولوحة الألوان `CampusPalette` وأصل URP. أرض وشبكة 14 × 12 والممر الرئيسي، ومبانٍ مؤقتة من أشكال بسيطة تظهر بحركة نمو، وكاميرا (WASD والأسهم وحواف الشاشة، وسحب بالزر الأيمن، وعجلة للتقريب، وQ/E للدوران)، ووضع البناء: المفاتيح 1–5 تختار المرفق، والنقر يرسل `Simulation.Build`، والرفض يهز الشبح باللون الأحمر، وN ينهي اليوم. لا يرى `SDT.World` النواة؛ يمر كل شيء عبر `CampusModel` في Bridge (مع `FacilityKind` مرآة لـ `Facility` يتحقق منها اختبار). نجحت 14 حالة EditMode. لقطة: [images/unity-campus-greybox.png](images/unity-campus-greybox.png). لم يُجرَّب اللعب التفاعلي (Play) بعد، ولا قياس معدل الإطارات.- واجهة HUD (2026-11-08): UI Toolkit (`Assets/_Project/UI/Hud.uxml` و`Hud.uss`) بتخطيط مرن قابل للانعكاس عبر الصنف `.rtl`. تعرض اليوم والرصيد والطلاب/السعة والسمعة والجودة وصافي أمس، وأزرار السرعة وEnd Day، وبطاقة قرار الحدث (تدخّل/تجاهل)، ونتيجة الحملة، وشريط البناء بالأسعار، ورسالة الرفض حمراء من `RuleCode`. كل النصوص من جدول `UI` في حزمة Localization (Smart Strings بمعاملات مسماة)؛ يزرع `LocalizationSeed` الإنجليزية دون الكتابة فوق التعديلات، واختبار يتحقق أن كل مفتاح تطلبه الواجهة موجود. تُحمَّل أوامر الإدخال من `Resources/SchoolDirectorTycoon.inputactions` لأن مرجعها في المشهد كان يُحفظ فارغًا عند توليد المشهد في batchmode. بناء Windows تطويري (`DevBuild.Windows`) يعمل، و`-sdtShot <ملف>` يلتقط صورة: [images/unity-hud.png](images/unity-hud.png). 16 اختبار EditMode ناجحة.
- مبنى الفصول المصقول (2026-11-08): `FacilityModels` في `SDT.World` يبني النموذج من صناديق ويدمجها في شبكة واحدة بشبكة فرعية لكل مادة (`OwnedMesh` يحرر الشبكة مع المبنى). يحتاج مواد `glass` و`trim` و`accent` في `CampusPalette`، وبدونها أو لبقية المرافق يعود إلى `Placeholders`. مدخل النموذج على -Z ويدور 180° للقطع تحت الممر. شبح البناء ما زال صندوقًا بسيطًا. لقطة: [images/unity-classroom.png](images/unity-classroom.png). 17 اختبار EditMode ناجحة.
- إكمال النموذج الأولي للحرم (2026-11-08): `FacilityModels` يغطي المرافق الخمسة، والشبح يستخدم النموذج الكامل. `CampusGridView.TerrainHeight` يولد كثبانًا ثابتة خارج الموقع بمادة `sand`. الظلال: 120 م و3 طبقات و4096. **الإدخال:** يجب أن يبقى `activeInputHandler: 1` (Input System) وأن يحوي المشهد `EventSystem` مع `InputSystemUIInputModule`، وإلا لا تستقبل أزرار HUD النقر. أدوات البناء التطويري: `-sdtPerf <ملف>` يملأ كل القطع ويسجل أزمنة الإطارات، و`-sdtPlayTest <ملف>` يلعب عبر الأوامر وHUD بفأرة ولوحة مفاتيح افتراضيتين. لقطة: [images/unity-facilities.png](images/unity-facilities.png).
- الطابع البصري (2026-11-08): الخامات من `CampusTextures` ومواد الأرض في `CampusPalette` (`HasLandscape`)، والحياة من `CampusLifeView` وهو عرض فقط ولا يكتب في الحالة. النماذج ما زالت مولدة بالكود من أشكال بسيطة وليست فنًا نهائيًا.
- الوضع النصي (2026-11-08): القواعد والأحداث في `src/SchoolDirectorTycoon.Core/Chronicle.cs`، والعرض عبر `ChronicleGame` في Bridge و`ChronicleController` في UI، والمشهد من `SDT/Rebuild Chronicle Scene`. الاختبار: `-sdtChronicle <تقرير> <صورة>` يلعب عامًا كاملًا بالأزرار الحقيقية. وحدة Android غير مثبتة في Unity بعد.
- تعمل اختبارات النواة على `net10.0` حاليًا، وتشغيلها على بناء `netstandard2.1` قيد الإضافة. سيتحقق اختبار التطابق داخل Unity من هدف `netstandard2.1`.

## مراحل العمل

1. **تجهيز النواة:** إضافة هدف `netstandard2.1` (مكتمل) وتمرير اختبارات النواة الحالية على الهدفين (قيد التنفيذ).
2. **نموذج الحرم:** مشهد واحد فيه تضاريس، وشبكة 14 × 12 وممر، ووضع مرفق من أوامر `Simulation.Build`، وزر End Day، وكاميرا علوية سلسة. قياس معدل الإطارات.
3. **لوحة الإدارة:** الطاقم والرسوم والتقارير والأحداث عبر جداول النصوص الإنجليزية.
4. **الحفظ:** الحفظ التلقائي والتحميل بنفس المخطط، وفتح ملفات حملات WPF.
5. **الجودة البصرية:** أصول مبانٍ وطبيعة، إضاءة نهار وليل، وطلاب على مسارات NavMesh.
6. **التعريب:** جدول العربية، وتشكيل الحروف، وانعكاس الواجهة، واختبار الاتجاهين.

## معيار القبول قبل إيقاف WPF

يكمل عميل Unity حملة 30 يومًا (النموذج الأولي فقط؛ النموذج طويل المدى في [نموذج البيانات 3](DataModelV3.md)) بنفس النتائج الرقمية لنفس البذرة والقرارات، ويفتح ملفات الحفظ الصادرة، ويعمل دون اتصال على Windows x64.

## نصوص العربية في UI Toolkit

- تتطلب العربية ثلاثة أشياء معًا: مفتاح `Enable Advanced Text Generator` في `ProjectSettings/UIToolkitProjectSettings.asset`، و`-unity-text-generator: advanced` في USS، و`languageDirection = RTL` على جذر الشاشة. غياب الأول يترك الحروف منفصلة، وغياب الأخير يضع الترقيم في الجهة الخاطئة.
- يفعّل `ChronicleSceneBuilder` المفتاح ويتحقق من ربط بيانات ICU بإعدادات اللوحة، ويحرس ذلك اختبار `ChroniclePanelCarriesArabicShapingData`.

## وضع حياة المدير

- `LifeController` يملك الوقت الحقيقي وملف `life.save` ويرسل الدقائق واليوم التقويمي إلى `SchoolLife` عبر `LifeGame`، فتبقى النواة قابلة لإعادة الإنتاج.
- `-sdtChronicle <report> <shot.png>` يلعب عامًا كاملًا عبر الأزرار ويحفظ صور الإعداد والرسالة والساعة والحدث والمساء والإعدادات والنهاية.
