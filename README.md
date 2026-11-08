# School Director Tycoon | مدير المدرسة

لعبة إدارة مدرسة خاصة على Windows تعمل دون اتصال. النسخة 0.3 تتضمن حملة قابلة للعب من 30 يومًا (نموذج أولي؛ الهدف طويل المدى أعوام دراسية متتالية في مدرسة خاصة قطرية، والعميل القادم على Unity 6)، وواجهة عربية وإنجليزية قابلة للتبديل من الشريط العلوي أثناء اللعب.

## تشغيل اللعبة

1. ثبّت **.NET Desktop Runtime 10.0.14** أو تصحيحًا أحدث من سلسلة 10.0، بالمعمارية الموافقة للنسخة. نسخة التسليم الحالية مبنية على Windows x64. [تنزيل Microsoft](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
2. فك ضغط `artifacts/SchoolDirectorTycoon-Windows-x64.zip` ثم شغّل `SchoolDirectorTycoon.Desktop.exe` من المجلد المستخرج مع الملفات المرافقة.
3. اختر مرفقًا من شريط الحرم ثم ضعْه على قطعة أرض في الخريطة. استخدم زرّي التكبير أو اسحب بزر الفأرة الأيمن بعد التكبير. عدّل الموظفين والرسوم، ثم اختر «إنهاء اليوم» أو شغّل الوقت. يكتب التطبيق الحفظ التلقائي بعد كل يوم مكتمل. عند إعادة فتح التطبيق استخدم أحد زري التحميل لاستئناف حملة محفوظة.

افتح «دليل البداية» من لوحة المدرسة لمعرفة أثر السعة والطاقم والرسوم على يومك الأول. تحفظ اللعبة لغة الواجهة محليًا وتستعيدها عند إعادة التشغيل.

## التطوير والتسليم

- [تسليم العمل للوكيل التالي](AGENTS.md)
- [تشغيل المصدر والاختبارات والنشر](Docs/Development.md)
- [الحرم والتحكم بالكاميرا](Docs/CampusWorld.md)
- [تصميم اللعبة ودليل الأنظمة](Docs/GameDesignDocument.md)
- [البنية ومسار الجوال](Docs/Architecture.md)
- [الانتقال إلى Unity](Docs/UnityMigration.md)
- [أداء المحاكاة والذاكرة](Docs/Performance.md)
- [الأفكار والأولويات](Docs/Roadmap.md)
- [سجل التغييرات](Docs/Changelog.md)

## English quick start

Install the Windows x64 .NET Desktop Runtime 10.0.14 or a later .NET 10 patch, extract the release ZIP, and launch `SchoolDirectorTycoon.Desktop.exe`. Select **English** in the top bar, then open **Getting started** on the dashboard. The language choice is remembered. Reach 65 students, 70 reputation, and a non-negative cash balance at the end of day 30. Use **Load autosave** to resume from the last completed day.
