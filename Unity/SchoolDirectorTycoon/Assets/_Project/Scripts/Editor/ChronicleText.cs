using System.Collections.Generic;

namespace SchoolDirectorTycoon.EditorTools
{
    /// <summary>English seed text for the week-by-week mode. Original writing; keys follow Core's event and choice ids.</summary>
    public static partial class ChronicleText
    {
        public static readonly Dictionary<string, string> English = Build();

        private static void Event(Dictionary<string, string> t, string id, string body, params string[][] choices)
        {
            t[$"ev.{id}"] = body;
            foreach (string[] c in choices)
            {
                t[$"ev.{id}.{c[0]}"] = c[1];
                t[$"ev.{id}.{c[0]}.ok"] = c[2];
                if (c.Length > 3) t[$"ev.{id}.{c[0]}.fail"] = c[3];
            }
        }

        private static string[] C(params string[] parts) => parts;

        private static Dictionary<string, string> Build()
        {
            var t = new Dictionary<string, string>
            {
                ["ch.title"] = "Al Noor Private School",
                ["ch.lang"] = "العربية",
                ["ch.week"] = "Week {week} of {total}",
                ["ch.cash"] = "{cash} QAR",
                ["ch.students"] = "{students} students",
                ["ch.next"] = "Next Week",
                ["ch.new"] = "Start a New Year",
                ["ch.decide"] = "Your decision",
                ["ch.log.week"] = "Week {week}",
                ["ch.log.start"] = "Before the bell",
                ["ch.delta"] = "{value} {name}",
                ["ch.stat.Cash"] = "QAR",
                ["ch.stat.Students"] = "Students",
                ["ch.stat.Reputation"] = "Reputation",
                ["ch.stat.Parents"] = "Parents",
                ["ch.stat.Staff"] = "Staff",
                ["ch.stat.Academics"] = "Academics",
                ["ch.ending.YearCompleted"] = "The year is done",
                ["ch.ending.Bankrupt"] = "Bankrupt",
                ["ch.ending.Dismissed"] = "The board fired you",
                ["ch.ending.StaffWalkout"] = "The staff walked out",
                ["ch.ending.body.YearCompleted"] = "The last bell rang on {students} students and a school still standing, which some weeks felt unlikely.",
                ["ch.ending.body.Bankrupt"] = "The salary account hit zero in week {week}. The sign outside is already for rent.",
                ["ch.ending.body.Dismissed"] = "In week {week} the board thanked you for your service and asked for your keys.",
                ["ch.ending.body.StaffWalkout"] = "In week {week} you arrived to find empty classrooms and a very short letter.",
                ["ch.grade"] = "Final grade: {grade}",
                ["story.opening"] = "You have the keys, 120 students and one very nervous accountant. Good luck, Director.",
                ["story.busy"] = "The week brought tuition, payroll and a knock on your door.",
                ["story.quiet.0"] = "A quiet week. You finally drank a coffee while it was hot.",
                ["story.quiet.1"] = "Nothing caught fire. You count this as a victory.",
                ["story.quiet.2"] = "The photocopier jammed twice. Otherwise, peace.",
                ["story.quiet.3"] = "Grade 3 sang in assembly. Most of them, in the same key.",
                ["story.quiet.4"] = "A parent emailed to say thank you. You printed it.",
                ["story.quiet.5"] = "The week passed with only minor arguments about parking.",
            };

            Event(t, "grade_favor", "A wealthy father slides an envelope across your desk and asks you to fix his son's maths mark.",
                C("refuse", "Hand back the envelope", "He left furious. The maths department bought you cake."),
                C("change", "Change the mark", "The envelope paid for new chairs. Nobody asked questions.", "The maths teacher took the marksheets to the board. It was a long meeting."));
            Event(t, "star_offer", "Your best physics teacher has an offer from a rival school, with a bigger salary.",
                C("raise", "Match the salary", "She stayed, and told everyone why."),
                C("promise", "Promise a raise next year", "She stayed for now. She has a calendar."),
                C("release", "Wish her well", "You saved her salary. Physics results may not recover."));
            Event(t, "exam_leak", "The Grade 10 chemistry paper is circulating in a student group chat.",
                C("retake", "Rewrite it and retake", "Parents groaned, but the results meant something."),
                C("silence", "Say nothing", "The marks looked wonderful. Nobody said a word.", "A parent posted the screenshots. The word 'scandal' was used."),
                C("investigate", "Find the source quietly", "You traced it to one phone and one apologetic prefect."));
            Event(t, "inspection", "A ministry inspector will arrive on Thursday.",
                R3("welcome", "Change nothing", "She praised how honestly the school runs.", "She found the fire door you meant to fix."),
                C("prepare", "Prepare all week", "Every file shone. So did the shadows under your teachers' eyes."),
                R3("delay", "Ask to postpone", "She agreed, and you all exhaled.", "She came anyway, and brought a colleague."));
            Event(t, "ac_failure", "The air conditioning has died in the east wing. It is 43 degrees outside.",
                C("repair", "Replace the unit", "Cold air returned. Grade 7 cheered as if for a goal."),
                C("patch", "Patch it for now", "It works, with a noise like a distant camel."),
                R3("wait", "Wait for cooler weather", "The heat broke early. You got lucky.", "Two students fainted and four families left."));
            Event(t, "bus_delay", "The morning bus has been late five days running, and the parents' group has noticed.",
                C("new_route", "Redraw the route", "The bus is on time. Parents have moved on to complaining about lunch."),
                C("apologize", "Send an apology", "Your apology was read. The bus was still late."),
                C("hire_driver", "Add a second driver", "Two drivers and no more delays."));
            Event(t, "tuition_debt", "A family has not paid in three months. The father lost his job.",
                C("grant", "Cover it with a bursary", "The mother cried in your office. Word got around."),
                C("instalments", "Offer instalments", "A fair plan, and the first payment arrived."),
                C("expel", "Remove the student", "The balance sheet improved. The staff room went very quiet."));
            Event(t, "donor_name", "A businessman offers a large gift, if the school is renamed after him.",
                C("accept", "Rename the school", "The money is real, and so is the new sign nobody likes."),
                C("plaque", "Offer a hall plaque", "He settled for a plaque and a smaller cheque."),
                C("decline", "Decline politely", "The staff were proud. The accountant lay down for a while."));
            Event(t, "bully_video", "A video of older boys tormenting a younger student is being shared.",
                C("suspend", "Suspend those involved", "A clear line was drawn, and one family left over it."),
                R3("mediate", "Bring the families together", "Hard words, then real apologies.", "The meeting ended with threats and a second video."),
                C("counselor", "Hire a counsellor", "The school has somewhere safe to send children now."));
            Event(t, "ramadan_hours", "Ramadan is coming. Parents and teachers want a shorter day.",
                C("shorten", "Shorten the day", "Everyone is grateful. The syllabus is a little behind."),
                C("keep", "Keep normal hours", "Lessons ran on time, through a great deal of yawning."),
                C("flex", "Pay for a flexible timetable", "A clever timetable kept nearly everybody happy."));
            Event(t, "national_day", "National Day is near. Grade 6 has already made too many flags.",
                C("big", "Hold a grand celebration", "Parents filmed everything. The school looked magnificent."),
                C("modest", "A modest assembly", "Short, warm and on budget."),
                C("skip", "Keep teaching", "A surprising number of people noticed."));
            Event(t, "science_fair", "The science teachers want to host a fair for local schools.",
                C("sponsor", "Fund it properly", "A Grade 9 water filter made the local news."),
                C("basic", "Keep it in-house", "Volcanoes erupted on schedule."),
                C("cancel", "Cancel it", "The budget held. The science staff did not smile."));
            Event(t, "teacher_conflict", "Two senior teachers are no longer speaking, and everyone has chosen a side.",
                C("hear", "Hear them both out", "It took an afternoon and a box of tissues. It worked."),
                C("order", "Order them to stop", "They stopped talking about it, and resent you instead."),
                C("transfer", "Rearrange departments", "Separate corridors solved what words could not."));
            Event(t, "parent_influencer", "A mother with 200,000 followers has posted about your school's uniform prices.",
                C("meet", "Invite her for coffee", "Her next post called you 'surprisingly reasonable'."),
                C("statement", "Publish a statement", "It was accurate and nobody read it."),
                R3("legal", "Have a lawyer write", "She deleted the post without a word.", "She posted the letter. It got more views than the first one."));
            Event(t, "lab_accident", "A small fire in the chemistry lab singed a bench and frightened Grade 8.",
                C("close_lab", "Close it for a full audit", "New equipment, new rules and no practicals for weeks."),
                C("train", "Run safety training", "Everyone now knows where the blanket is."),
                R3("carry_on", "Carry on as normal", "It was a one-off, and practicals went on.", "It was not a one-off. An ambulance came."));
            Event(t, "rival_poach", "A new international school is offering discounts to your families.",
                C("scholarship", "Launch scholarships", "Three bright students joined, and the newspapers noticed."),
                C("open_day", "Hold an open day", "Families saw what they would be leaving."),
                C("ignore", "Do nothing", "Five desks are empty on Sunday morning."));
            Event(t, "canteen_health", "A nutritionist parent has photographed everything sold in your canteen.",
                C("healthy", "Change the menu", "Fruit now outsells chips, by a slim margin."),
                C("warn", "Ask the vendor to improve", "He added one apple to the display."),
                C("change_vendor", "Bring in a new vendor", "Better food, and a very upset former vendor."));
            Event(t, "cheating_son", "The chairman's son was caught cheating in a major exam.",
                C("zero", "Give him a zero", "The chairman stopped greeting you. The teachers started."),
                C("warn", "Give a quiet warning", "He sat the paper again. The staff noticed who gets second chances."),
                R3("cover", "Make it disappear", "The chairman was very grateful.", "The invigilator resigned and explained why, in writing."));
            Event(t, "teacher_burnout", "Three teachers have asked for sick leave in the same week.",
                C("relief", "Hire relief staff", "Help arrived and shoulders dropped."),
                C("day_off", "Give everyone a day off", "A deep breath, and a lost day of lessons."),
                C("push", "Remind them of their contracts", "They came back and stopped staying late."));
            Event(t, "olympiad", "Two students could compete at the national maths olympiad.",
                C("coach", "Pay for a coach", "They returned with bronze and a lot of confidence."),
                R3("send", "Send them as they are", "They took silver. You did nothing and accepted all the credit.", "They came last, but kindly."),
                C("pass", "Skip it this year", "The students were quiet about it."));
            Event(t, "sandstorm", "A heavy sandstorm is forecast for tomorrow morning.",
                C("close", "Close the school", "Everybody stayed safe at home."),
                C("online", "Teach online", "The lesson went ahead, with several cats."),
                R3("open", "Stay open", "The storm passed north, and you looked decisive.", "Buses crawled through it. Parents were furious."));
            Event(t, "arabic_curriculum", "Parents are divided about how much of the day should be taught in Arabic.",
                C("expand", "Strengthen Arabic", "Grandparents everywhere approved."),
                C("balance", "Keep the balance", "A committee will continue to discuss it."),
                C("english", "Move further to English", "A cheaper timetable and some very sharp emails."));
            Event(t, "tablet_deal", "A salesman offers a tablet for every student, payable now.",
                C("buy", "Buy them all", "Lessons changed, and so did the IT teacher's sleep."),
                C("pilot", "Pilot with one grade", "A careful trial with good results."),
                C("decline", "Stick with books", "Paper has never needed a software update."));
            Event(t, "alumni_visit", "A former student, now a surgeon, wants to visit her old school.",
                C("host", "Hold a welcome assembly", "She remembered her Grade 4 teacher's name. Nobody's eyes stayed dry."),
                R3("ask_fund", "Ask her for a donation", "She wrote a generous cheque on the spot.", "She left early, and rather politely."),
                C("skip", "Say you are too busy", "She sent a kind note anyway."));
            Event(t, "salary_late", "A banking error means salaries will be late this month.",
                C("loan", "Take a short bridging loan", "Everyone was paid on time and never knew."),
                C("explain", "Explain and apologise", "They understood. Rent collectors did not."),
                C("cut_trip", "Cancel the Grade 8 trip", "Salaries were paid. Grade 8 will remember this."));
            Event(t, "special_needs", "A family asks if your school can support their son, who uses a wheelchair.",
                C("adapt", "Rebuild for access", "Ramps went in. He is now the loudest boy in Grade 5."),
                C("partial", "Make what changes you can", "He joined, with some awkward routes around the building."),
                C("decline", "Say you cannot", "The family told their story to anyone who asked."));
            Event(t, "football_final", "The school team has reached the league final.",
                C("bus_fans", "Bus everyone to the match", "They lost on penalties. It was the best day of the year."),
                C("cheer", "Wish them luck", "Polite applause from the staff room."),
                C("exams_first", "Exams come first", "Marks rose. So did the resentment."));
            Event(t, "roof_leak", "Rare rain has found every crack in the old classroom roof.",
                C("replace", "Replace the roof", "Expensive, dry and finally finished."),
                R3("cheap", "Hire the cheapest contractor", "It held. You are as surprised as anyone.", "It leaked again, onto the new projector."),
                C("move_class", "Move the classes", "Grade 5 now studies in the library, loudly."));
            Event(t, "gossip_group", "The parents' group chat has decided you are hiding something.",
                C("join", "Join the group", "You now receive 400 messages a day, and they trust you."),
                C("newsletter", "Start a weekly newsletter", "Facts have begun to compete with rumours."),
                C("ban", "Forbid staff from replying", "The group noticed, and added it to the list."));
            Event(t, "final_exams", "Final exams are close and Grade 12 looks unready.",
                C("revision", "Fund extra revision", "Teachers are exhausted. Grade 12 is not lost."),
                C("normal", "Trust the teaching", "They will get what they have earned."),
                R3("easy_paper", "Make the papers easier", "Everyone went home happy.", "External moderators asked to see the papers."));
            AddLifeEnglish(t);
            return t;
        }

        private static string[] R3(params string[] parts) => parts;
    }
}
