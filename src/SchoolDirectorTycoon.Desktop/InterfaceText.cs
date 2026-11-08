namespace SchoolDirectorTycoon.Desktop;

internal static class InterfaceText
{
    internal static readonly IReadOnlyDictionary<string, (string Ar, string En)> All =
        new Dictionary<string, (string Ar, string En)>
        {
            ["Title"] = ("مدير المدرسة", "School Director Tycoon"),
            ["Goal"] = ("مدرسة خاصة • حملة من 30 يومًا • الهدف 65 طالبًا وسمعة 70 مع رصيد غير سالب", "Private school • 30-day campaign • Reach 65 students and 70 reputation with cash at or above zero"),
            ["New"] = ("حملة جديدة", "New campaign"),
            ["Save"] = ("حفظ يدوي", "Save manually"),
            ["Load"] = ("تحميل يدوي", "Load manual save"),
            ["Auto"] = ("تحميل تلقائي", "Load autosave"),
            ["Next"] = ("إنهاء اليوم", "End day"),
            ["SpeedLabel"] = ("سرعة الوقت", "Time speed"),
            ["Pause"] = ("متوقف", "Paused"),
            ["Decision"] = ("قرار مطلوب", "Decision required"),
            ["Ignore"] = ("تابع دون تدخل", "Continue without intervention"),
            ["Manage"] = ("إدارة المدرسة", "School management"),
            ["Tuition"] = ("الرسوم اليومية", "Daily tuition"),
            ["Apply"] = ("تطبيق", "Apply"),
            ["Finance"] = ("التقارير المالية والتسجيل", "Finance and enrollment reports"),
            ["CampusHeading"] = ("الحرم المدرسي", "School grounds"),
            ["ZoomIn"] = ("تكبير الخريطة", "Zoom in"),
            ["ZoomOut"] = ("تصغير الخريطة", "Zoom out"),
            ["ResetCamera"] = ("إعادة العرض", "Reset view"),
            ["CancelBuild"] = ("إلغاء البناء", "Cancel construction"),
            ["StarterGuide"] = ("دليل البداية", "Getting started"),
            ["GuideCapacity"] = ("١. السعة • يضيف الفصل 24 مقعدًا. اختر المرفق من شريط الحرم ثم اختر موقعه على الخريطة.", "1. Capacity • A classroom adds 24 seats. Choose a facility from the grounds toolbar, then pick its location on the map."),
            ["GuideStaff"] = ("٢. الطاقم • يغطي المعلم 16 طالبًا. عندما يتجاوز التسجيل تغطية الطاقم، تنخفض الجودة وقد يغادر طلاب.", "2. Staff • Each teacher covers 16 students. Enrollment above staff coverage reduces quality and may cause departures."),
            ["GuideTuition"] = ("٣. الرسوم • الرسوم المرتفعة تزيد دخل الطالب الواحد، لكنها تضعف الطلب وقد تزيد المغادرة. قارن النتائج في التقارير.", "3. Tuition • Higher fees increase revenue per student but reduce demand and can increase departures. Compare the results in the reports."),
            ["GuideDay"] = ("٤. الوقت • استخدم «إنهاء اليوم» لمراجعة نتيجة يوم واحد. عند تشغيل الوقت يتوقف تلقائيًا إذا احتاج حدث إلى قرار.", "4. Time • Use End day to review one day at a time. Automatic time pauses when an incident needs a decision."),
            ["GuideReport"] = ("٥. التقارير • قارن الإيرادات بالأجور والتشغيل. يظهر شراء المرافق ضمن الاستثمار، ويمكنك استئناف آخر يوم مكتمل من الحفظ التلقائي.", "5. Reports • Compare income with payroll and operating costs. Facility purchases appear as investment. Load the autosave to resume after the last completed day."),
            ["ReportsLabel"] = ("تقارير الأيام", "Daily reports"),
        };
}
