using System.Collections.Generic;

namespace SchoolDirectorTycoon.EditorTools
{
    /// <summary>Text of School Life v2, in both languages. Names may belong to anyone, so lines that hold {name} avoid gendered wording.</summary>
    public static partial class ChronicleText
    {
        private static void AddLife2English(Dictionary<string, string> t)
        {
            t["life.name.0"] = "Ahmed";
            t["life.name.1"] = "Maryam";
            t["life.name.2"] = "Khalid";
            t["life.name.3"] = "Fatima";
            t["life.name.4"] = "Hamad";
            t["life.name.5"] = "Aisha";
            t["life.name.6"] = "Jassim";
            t["life.name.7"] = "Latifa";
            t["life.name.8"] = "Abdulla";
            t["life.name.9"] = "Moza";
            t["life.name.10"] = "Saeed";
            t["life.name.11"] = "Shaikha";
            t["life.name.12"] = "Faisal";
            t["life.name.13"] = "Hessa";
            t["life.name.14"] = "Nasser";
            t["life.name.15"] = "Amna";
            t["life.name.16"] = "Mubarak";
            t["life.name.17"] = "Sara";
            t["life.name.18"] = "Rashid";
            t["life.name.19"] = "Dana";
            t["life.name.20"] = "Ali";
            t["life.name.21"] = "Reem";
            t["life.name.22"] = "Sultan";
            t["life.name.23"] = "Noof";

            t["life.subject.arabic"] = "Arabic";
            t["life.subject.english"] = "English";
            t["life.subject.math"] = "Maths";
            t["life.subject.science"] = "Science";
            t["life.subject.islamic"] = "Islamic studies";
            t["life.subject.social"] = "Social studies";
            t["life.subject.computing"] = "Computing";
            t["life.subject.sports"] = "Sports";

            t["life.trait.punctual"] = "Punctual";
            t["life.trait.punctual.info"] = "Always in the room before the bell. The bell has started to feel judged.";
            t["life.trait.late"] = "Often late";
            t["life.trait.late.info"] = "Blames the traffic, even on days with no traffic. Some lessons start without a teacher.";
            t["life.trait.patient"] = "Patient";
            t["life.trait.patient.info"] = "Will explain fractions a ninth time in the same calm voice as the first.";
            t["life.trait.strict"] = "Strict";
            t["life.trait.strict.info"] = "Silent classrooms, straight rows and the occasional complaint from home.";
            t["life.trait.creative"] = "Creative";
            t["life.trait.creative.info"] = "Taught the water cycle with a kettle and a song. Now and then it lifts the whole school.";
            t["life.trait.ambitious"] = "Ambitious";
            t["life.trait.ambitious.info"] = "Has already asked what your job pays. Learns fast and expects to be noticed.";

            t["life.interview.reply.experience.punctual"] = "In eleven years I have never needed a bell to tell me where to be.";
            t["life.interview.reply.experience.late"] = "Three schools, all excellent. Sorry again about this morning, the roundabout was a disgrace.";
            t["life.interview.reply.experience.patient"] = "I once spent a whole term on one boy and long division. We got there in May.";
            t["life.interview.reply.experience.strict"] = "My classes finish the syllabus early. Nobody has ever asked me why they are so quiet.";
            t["life.interview.reply.experience.creative"] = "I have taught in six rooms and rearranged the furniture in all of them.";
            t["life.interview.reply.experience.ambitious"] = "Four years as a teacher, one as head of department. I am early for the next step.";
            t["life.interview.reply.discipline.punctual"] = "I am at the door when they arrive. It is difficult to be late to someone already waiting.";
            t["life.interview.reply.discipline.late"] = "Children settle down on their own, I find, if you give them ten minutes before you get there.";
            t["life.interview.reply.discipline.patient"] = "I wait. Nobody can stay angry at a person who is simply waiting.";
            t["life.interview.reply.discipline.strict"] = "One warning. I have rarely needed to explain what comes after it.";
            t["life.interview.reply.discipline.creative"] = "The loudest child gets to run the experiment. Suddenly the loudest child wants silence.";
            t["life.interview.reply.discipline.ambitious"] = "I keep records of everything. Results improve, and I can show you the graph.";
            t["life.interview.reply.dream.punctual"] = "A timetable that holds for one full week. I would frame it.";
            t["life.interview.reply.dream.late"] = "A house next door to the school. Possibly inside the school.";
            t["life.interview.reply.dream.patient"] = "To meet one of my old students who still remembers how to do fractions.";
            t["life.interview.reply.dream.strict"] = "An exam hall where nobody asks to borrow a ruler.";
            t["life.interview.reply.dream.creative"] = "A classroom without walls. Failing that, one with a very large window.";
            t["life.interview.reply.dream.ambitious"] = "Your chair, Director, in about five years. I mean that respectfully.";

            t["life.case.fight"] = "A fight in the yard after break, with {name} in the middle of it. One torn shirt and three versions of events.";
            t["life.case.fight.punish"] = "Suspend for a day";
            t["life.case.fight.talk"] = "Sit down and listen";
            t["life.case.fight.parents"] = "Call the family in";
            t["life.case.fight.punish.ok"] = "{name} spent a day at home. The yard was quiet, and the shirt was not mentioned again.";
            t["life.case.fight.talk.ok"] = "You heard {name} out over a cup of water. There turned out to be a fourth version of events.";
            t["life.case.fight.parents.ok"] = "The family of {name} arrived in force. Everyone apologised to everyone, at length.";
            t["life.case.phone"] = "A phone rang in the middle of a science test, and it belongs to {name}. The ringtone was, unfortunately, memorable.";
            t["life.case.phone.punish"] = "Confiscate it for a week";
            t["life.case.phone.talk"] = "Have a quiet word";
            t["life.case.phone.parents"] = "Hand it to the family";
            t["life.case.phone.punish.ok"] = "The phone of {name} now lives in your drawer, where it receives many messages.";
            t["life.case.phone.talk.ok"] = "A short talk with {name}, and the phone went back in the bag, on silent, for now.";
            t["life.case.phone.parents.ok"] = "The phone went home by way of the family of {name}. Word is it stayed there.";
            t["life.case.cheating"] = "The maths teacher has two identical papers, down to the same wrong answer. One of them is by {name}.";
            t["life.case.cheating.punish"] = "Zero on the paper";
            t["life.case.cheating.talk"] = "Ask for the truth";
            t["life.case.cheating.parents"] = "Inform the family";
            t["life.case.cheating.punish.ok"] = "{name} received a zero, written very clearly. The other paper got the same.";
            t["life.case.cheating.talk.ok"] = "It took a long silence, and then {name} told you everything. A retest was agreed.";
            t["life.case.cheating.parents.ok"] = "You showed both papers to the family of {name}. Nobody needed the mistake explained.";
            t["life.case.skipped"] = "The register says {name} was absent. The guard saw someone very similar at the corner shop at ten, holding a karak.";
            t["life.case.skipped.punish"] = "Detention all week";
            t["life.case.skipped.talk"] = "Ask what is going on";
            t["life.case.skipped.parents"] = "Telephone home";
            t["life.case.skipped.punish.ok"] = "{name} spent the week in detention, and discovered that the library has a clock that ticks.";
            t["life.case.skipped.talk.ok"] = "{name} was avoiding one particular lesson. You now know which one, and why.";
            t["life.case.skipped.parents.ok"] = "The phone call to the home of {name} was short. The next morning's arrival was very early.";
            t["life.case.rude"] = "{name} answered a teacher back in front of the whole class, and the staff room is still discussing the wording.";
            t["life.case.rude.punish"] = "Written apology and detention";
            t["life.case.rude.talk"] = "Talk it through";
            t["life.case.rude.parents"] = "Bring the family in";
            t["life.case.rude.punish.ok"] = "{name} wrote the apology in full. The grammar, the teacher noted, was excellent.";
            t["life.case.rude.talk.ok"] = "You asked {name} what had really happened that day. The answer had little to do with the teacher.";
            t["life.case.rude.parents.ok"] = "The family of {name} sat in your office and agreed with the teacher, loudly.";
            t["life.case.late_again"] = "{name} has been late four times in two weeks. The excuses have become quite inventive.";
            t["life.case.late_again.punish"] = "Stay after the last bell";
            t["life.case.late_again.talk"] = "Ask about mornings";
            t["life.case.late_again.parents"] = "Speak to the family";
            t["life.case.late_again.punish.ok"] = "{name} now stays behind after school, making up each minute under the eye of the guard.";
            t["life.case.late_again.talk.ok"] = "There are younger siblings to be walked to another school. You and {name} worked out a new start time.";
            t["life.case.late_again.parents.ok"] = "The family of {name} promised an alarm clock. Two were delivered.";
            t["life.case.vandal"] = "Someone carved a name into a new desk with a compass. The name is {name}, which at least saved time.";
            t["life.case.vandal.punish"] = "Pay for the desk";
            t["life.case.vandal.talk"] = "Sand it down together";
            t["life.case.vandal.parents"] = "Show the family the desk";
            t["life.case.vandal.punish.ok"] = "{name} paid for the desk from a month of pocket money, in instalments.";
            t["life.case.vandal.talk.ok"] = "You and {name} spent an afternoon with sandpaper. The desk looks fine. The talk was better.";
            t["life.case.vandal.parents.ok"] = "The family of {name} looked at the desk for some time, then offered to buy it.";
            t["life.case.bullied"] = "A note has reached your office: {name} has been eating lunch alone in the stairwell, and some classmates have made sure of it.";
            t["life.case.bullied.punish"] = "Punish those responsible";
            t["life.case.bullied.talk"] = "Sit with the student";
            t["life.case.bullied.parents"] = "Call both families";
            t["life.case.bullied.punish.ok"] = "The ringleaders lost a month of break. {name} ate in the yard the next day, cautiously.";
            t["life.case.bullied.talk.ok"] = "You sat with {name} on the stairs during lunch. You did not say much, and that seemed to help.";
            t["life.case.bullied.parents.ok"] = "Two families met in your office with {name} in mind. It was uncomfortable, and it needed to be.";
            t["life.case.followup.punish.good"] = "A week on, {name} has stayed out of trouble. The lesson seems to have held.";
            t["life.case.followup.punish.bad"] = "A week on, {name} is back in the corridor with a new grievance. Punishment did not reach far.";
            t["life.case.followup.talk.good"] = "A week on, {name} stopped by your door just to say good morning.";
            t["life.case.followup.talk.bad"] = "A week on, {name} nods politely and has changed nothing. Talking was the easy part.";
            t["life.case.followup.parents.good"] = "A week on, the homework of {name} arrives signed and on time. Someone at home is watching.";
            t["life.case.followup.parents.bad"] = "A week on, things at home are tense, and {name} has brought them to school.";

            t["life.action.title"] = "Director's day";
            t["life.action.gate"] = "Morning at the gate with the parents";
            t["life.action.broadcast_kindness"] = "Morning broadcast on kindness";
            t["life.action.broadcast_study"] = "Morning broadcast on study";
            t["life.action.broadcast_discipline"] = "Morning broadcast on discipline";
            t["life.action.observe"] = "You sat at the back of a lesson by {name}, notebook closed.";

            t["life.up.ac"] = "New air conditioning";
            t["life.up.ac.info"] = "A cool building forgives a great deal. Softens one parent complaint each week.";
            t["life.up.ac.done"] = "The new air conditioning is running. Nobody has mentioned the heat, which is the highest praise available.";
            t["life.up.canteen"] = "Proper canteen";
            t["life.up.canteen.info"] = "Hot food at fair prices, and steady income every week.";
            t["life.up.canteen.done"] = "The canteen has opened. The samosa shortage is officially over.";
            t["life.up.library"] = "Library";
            t["life.up.library.info"] = "Shelves, quiet and a slow, reliable rise in academics.";
            t["life.up.library.done"] = "The library is open. The first book was borrowed within four minutes.";
            t["life.up.lab"] = "Science lab";
            t["life.up.lab.info"] = "Real experiments, and steadily better academics.";
            t["life.up.lab.done"] = "The lab is ready. Please note the location of the fire blanket.";
            t["life.up.playground"] = "Shaded playground";
            t["life.up.playground.info"] = "Children come home tired and happy. Parents notice.";
            t["life.up.playground.done"] = "The playground is finished. Break now feels shorter to everyone but the teachers.";
            t["life.up.garden"] = "Front garden";
            t["life.up.garden.info"] = "Passers-by start to talk about the school in a kinder voice.";
            t["life.up.garden.done"] = "The garden is planted. Three teachers have told you about it already.";
            t["life.up.bus"] = "School buses";
            t["life.up.bus.info"] = "Families further away can consider you now. More students apply.";
            t["life.up.bus.done"] = "The buses have arrived, freshly painted. The drivers have already formed opinions about the car park.";
            t["life.up.training_center"] = "Training centre";
            t["life.up.training_center.info"] = "Send teachers on courses, right here on the grounds. Two places.";
            t["life.up.training_center.done"] = "The training centre is open. The whiteboard is new, and the markers still work.";
            t["life.up.training_center2"] = "Training centre extension";
            t["life.up.training_center2.info"] = "Two more places, so the staff room need not empty all at once.";
            t["life.up.training_center2.done"] = "The extension is ready. Four teachers can now be improved at the same time.";
            t["life.up.wing"] = "New wing";
            t["life.up.wing.info"] = "A whole new wing, sixty more seats, and a great deal of dust.";
            t["life.up.wing.done"] = "The new wing is finished. Sixty empty chairs are waiting, and the paint is nearly dry.";

            t["life.course.method"] = "Teaching methods";
            t["life.course.method.info"] = "A solid refresher. The teacher comes back with new habits.";
            t["life.course.method.done"] = "The teaching methods course is over, and {name} is back in {subject} lessons with fresh ideas.";
            t["life.course.subject"] = "Subject mastery";
            t["life.course.subject.info"] = "Deep study of the teacher's own subject. A long absence, a large gain.";
            t["life.course.subject.done"] = "The subject mastery course is over. {name} now knows more {subject} than the textbook does.";
            t["life.course.master"] = "Master teacher programme";
            t["life.course.master.info"] = "The hardest course offered. The best results, and a tired teacher afterwards.";
            t["life.course.master.done"] = "The master programme is over. {name} is back in {subject}, exhausted and formidable.";
            t["life.course.care"] = "Staff wellbeing";
            t["life.course.care.info"] = "Short, cheap and restful. Teaches nothing and repairs a lot.";
            t["life.course.care.done"] = "The wellbeing course is over, and {name} returned to {subject} lessons smiling, which alarmed the students.";

            t["life.school.0"] = "Al Fanar School";
            t["life.school.1"] = "Al Sidra School";
            t["life.school.2"] = "Al Bidaa School";
            t["life.school.3"] = "Al Maha School";
            t["life.school.4"] = "Al Dana School";
            t["life.school.5"] = "Al Ghaf School";
            t["life.school.6"] = "Al Shamal School";
            t["life.school.7"] = "Al Rayyan Gate School";

            t["life.crest.0"] = "Falcon";
            t["life.crest.1"] = "Palm";
            t["life.crest.2"] = "Dhow";
            t["life.crest.3"] = "Lamp";
            t["life.crest.4"] = "Crescent";
            t["life.crest.5"] = "Book";

            t["life.phil.strict"] = "Rigour first";
            t["life.phil.strict.info"] = "High standards and good results. Some parents will find it a little much.";
            t["life.phil.creative"] = "Curiosity first";
            t["life.phil.creative.info"] = "Teachers love the freedom. Exam marks occasionally pay for it.";
            t["life.phil.sports"] = "Body and mind";
            t["life.phil.sports.info"] = "Parents love a school with a team. Lesson time sometimes loses out.";
            t["life.phil.faith"] = "Faith and character";
            t["life.phil.faith.info"] = "Manners, values and the steady trust of families.";

            t["life.act.summer_camp"] = "Run a summer camp, {cash} QAR";
            t["life.act.summer_camp.ok"] = "The camp filled every room. Teachers who wished for a quiet summer are not speaking to you.";
            t["life.act.adult_courses"] = "Host adult evening classes, {cash} QAR";
            t["life.act.adult_courses.ok"] = "Grown men squeezed into small desks to learn spreadsheets. They paid on time.";

            t["life.loan.button"] = "Take a bank loan";
            t["life.loan.taken"] = "The bank agreed quickly, which worried you. Repayments now come out every week.";
            t["life.loan.paid"] = "The last repayment has gone through. The bank sent a card. You did not frame it.";

            t["life.rival.30"] = "A new academy has opened two streets away, with a banner taller than your building. Parents are asking questions.";
            t["life.rival.30.scholarship"] = "Offer scholarships";
            t["life.rival.30.open_day"] = "Hold an open day";
            t["life.rival.30.ignore"] = "Pay it no attention";
            t["life.rival.30.scholarship.ok"] = "Your scholarships were the talk of the neighbourhood for a week. The banner looked slightly smaller.";
            t["life.rival.30.open_day.ok"] = "Families toured your classrooms, ate your biscuits and stayed to chat.";
            t["life.rival.30.ignore.ok"] = "You kept your eyes on your own school. The banner stayed where it was.";
            t["life.rival.60"] = "The academy is now offering free tablets and a parent app. Some of your families have stopped meeting your eye at the gate.";
            t["life.rival.60.scholarship"] = "Fund more scholarships";
            t["life.rival.60.open_day"] = "Open the doors again";
            t["life.rival.60.ignore"] = "Trust your reputation";
            t["life.rival.60.scholarship.ok"] = "It cost a painful amount. Several families who had packed their files unpacked them.";
            t["life.rival.60.open_day.ok"] = "Your old students came back to speak. No tablet has yet managed that.";
            t["life.rival.60.ignore.ok"] = "You said nothing and carried on. A few more desks stand empty at the back.";

            t["life.season.exams_mid"] = "Midterm exams begin. The corridors have gone suspiciously tidy.";
            t["life.season.exams_mid.banner"] = "Midterm exams";
            t["life.season.ramadan"] = "Ramadan has come. Shorter days, softer voices and a canteen with nothing to do.";
            t["life.season.ramadan.banner"] = "Ramadan Kareem";
            t["life.season.national"] = "National day. Flags on every window, and a parade that was almost in step.";
            t["life.season.national.banner"] = "National day";
            t["life.season.exams_final"] = "Final exams. Nobody is borrowing a ruler without a very good reason.";
            t["life.season.exams_final.banner"] = "Final exams";

            t["life.ministry"] = "An inspector from the ministry is at the gate, with a thick file and no appointment. You have ten minutes.";
            t["life.ministry.show"] = "Show the best classes";
            t["life.ministry.show.ok"] = "The inspector saw your finest lessons and wrote for a long time, nodding.";
            t["life.ministry.show.fail"] = "The inspector opened one door you had not chosen. The note was brief, and it was underlined.";
            t["life.ministry.prepare"] = "Hand over the records";
            t["life.ministry.prepare.ok"] = "Every file was in order. The inspector looked almost disappointed, then signed.";
            t["life.ministry.prepare.fail"] = "The records were neat, and the results inside them were not. The inspector turned each page slowly.";
            t["life.ministry.honest"] = "Tell the whole truth";
            t["life.ministry.honest.ok"] = "You listed your own weaknesses before being asked. The inspector closed the file and asked for tea.";
            t["life.ministry.honest.fail"] = "You were honest, and the inspector was grateful, and wrote down all of it.";

            t["life.grad.body"] = "The last evening of the year. Chairs in rows, gowns slightly too long, and every parent holding a phone up. They are waiting for you to speak.";
            t["life.grad.thanks"] = "Thank the teachers";
            t["life.grad.thanks.ok"] = "You read the name of every teacher and the room stood up. A few of them pretended to have something in their eye.";
            t["life.grad.dreams"] = "Speak of their futures";
            t["life.grad.dreams.ok"] = "You told them the world was smaller than it looks and theirs to cross. Several parents wrote that down.";
            t["life.grad.duty"] = "Speak of duty";
            t["life.grad.duty.ok"] = "You asked them to come back one day and leave this city better. The hall went quiet, and stayed that way.";

            t["life.rank"] = "City rank: {rank} of 10";

            t["life.goal.title"] = "Today's goal";
            t["life.goal.no_low_mood"] = "Keep every teacher in a decent mood";
            t["life.goal.reach_parents"] = "Get the parents on your side";
            t["life.goal.earn_tonight"] = "Earn something tonight";
            t["life.goal.visit_gate"] = "Greet the parents at the gate";
            t["life.goal.observe_teacher"] = "Sit in on a lesson";
            t["life.goal.done"] = "Goal met. A small bonus has found its way to the accounts.";

            t["life.ach.title"] = "Achievements";
            t["life.ach.no_dismissals"] = "Nobody left";
            t["life.ach.no_dismissals.info"] = "Finish the year without dismissing a single teacher.";
            t["life.ach.full_house"] = "Full house";
            t["life.ach.full_house.info"] = "Fill every seat in the school.";
            t["life.ach.first_upgrade"] = "Under construction";
            t["life.ach.first_upgrade.info"] = "Complete your first building project.";
            t["life.ach.trained_five"] = "Staff college";
            t["life.ach.trained_five.info"] = "Send five teachers through a course.";
            t["life.ach.rank_one"] = "Best in the city";
            t["life.ach.rank_one.info"] = "Reach first place in the city ranking.";
            t["life.ach.streak_seven"] = "Seven mornings";
            t["life.ach.streak_seven.info"] = "Read the morning letter seven days in a row.";

            t["life.memories.title"] = "Memories";

            t["life.tab.school"] = "School";
            t["life.tab.people"] = "People";
            t["life.tab.build"] = "Build";
            t["life.tab.train"] = "Train";
            t["life.tab.office"] = "Office";

            t["life.people.title"] = "Your staff";
            t["life.people.hire"] = "Interview candidates";
            t["life.people.dismiss"] = "Dismiss";
            t["life.people.observe"] = "Observe a lesson";
            t["life.people.skill"] = "Skill";
            t["life.people.mood"] = "Mood";
            t["life.people.pay"] = "Weekly pay";
            t["life.people.trait.unknown"] = "Not yet known";
            t["life.people.in_training"] = "On a course";

            t["life.interview.title"] = "Interviews";
            t["life.interview.ask.experience"] = "Tell me about your experience.";
            t["life.interview.ask.discipline"] = "How do you handle a difficult class?";
            t["life.interview.ask.dream"] = "What do you dream of?";
            t["life.interview.hire"] = "Hire";
            t["life.interview.cancel"] = "Send them all home";
            t["life.interview.band.low"] = "Seems inexperienced";
            t["life.interview.band.mid"] = "Seems capable";
            t["life.interview.band.high"] = "Seems excellent";

            t["life.build.title"] = "Building projects";
            t["life.build.cost"] = "Cost";
            t["life.build.time"] = "Time";
            t["life.build.building"] = "Under construction";
            t["life.build.done"] = "Built";
            t["life.build.start"] = "Start work";

            t["life.train.title"] = "Training centre";
            t["life.train.slots"] = "Places";
            t["life.train.start"] = "Send on course";
            t["life.train.need_center"] = "Build the training centre first.";
            t["life.train.remaining"] = "Time left";

            t["life.time.remaining"] = "{hours}h {minutes}m left";
            t["life.time.duration"] = "{hours}h {minutes}m";
            t["life.clock.extend"] = "Extend today";
            t["life.clock.shorten"] = "Shorten today";

            t["life.identity.title"] = "Your school's identity";
            t["life.identity.name"] = "School name";
            t["life.identity.crest"] = "Crest";
            t["life.identity.colour"] = "Colour";
            t["life.identity.philosophy"] = "Philosophy";

            t["life.form.period_minutes"] = "Minutes per lesson";
            t["life.form.break_minutes"] = "Minutes of break";

            t["life.students.title"] = "Students to watch";
            t["life.students.marks"] = "Marks";
            t["life.students.conduct"] = "Conduct";

            t["rule.LifeTeacherBusy"] = "That teacher is away on a course.";
            t["rule.LifeNoCandidate"] = "That candidate is not available.";
            t["rule.LifeUpgradeBusy"] = "A building project is already under way.";
            t["rule.LifeUpgradeDone"] = "That has already been built.";
            t["rule.LifeUpgradeLocked"] = "An earlier project must be finished first.";
            t["rule.LifeNoTrainingSlot"] = "There are no free places on courses.";
            t["rule.LifeNeedsTrainingCenter"] = "This needs a training centre.";
            t["rule.LifeLoanActive"] = "You are still repaying a loan.";
            t["rule.LifeActionUsed"] = "You have already done that today.";
            t["rule.LifeCasePending"] = "Deal with the matter on your desk first.";
            t["rule.LifeLastTeacher"] = "The school cannot run without a teacher.";
            t["rule.LifeDayLimit"] = "The school day cannot change any further.";
        }

        private static void AddLife2Arabic(Dictionary<string, string> t)
        {
            t["life.name.0"] = "أحمد";
            t["life.name.1"] = "مريم";
            t["life.name.2"] = "خالد";
            t["life.name.3"] = "فاطمة";
            t["life.name.4"] = "حمد";
            t["life.name.5"] = "عائشة";
            t["life.name.6"] = "جاسم";
            t["life.name.7"] = "لطيفة";
            t["life.name.8"] = "عبدالله";
            t["life.name.9"] = "موزة";
            t["life.name.10"] = "سعيد";
            t["life.name.11"] = "شيخة";
            t["life.name.12"] = "فيصل";
            t["life.name.13"] = "حصة";
            t["life.name.14"] = "ناصر";
            t["life.name.15"] = "آمنة";
            t["life.name.16"] = "مبارك";
            t["life.name.17"] = "سارة";
            t["life.name.18"] = "راشد";
            t["life.name.19"] = "دانة";
            t["life.name.20"] = "علي";
            t["life.name.21"] = "ريم";
            t["life.name.22"] = "سلطان";
            t["life.name.23"] = "نوف";

            t["life.subject.arabic"] = "اللغة العربية";
            t["life.subject.english"] = "اللغة الإنجليزية";
            t["life.subject.math"] = "الرياضيات";
            t["life.subject.science"] = "العلوم";
            t["life.subject.islamic"] = "التربية الإسلامية";
            t["life.subject.social"] = "الدراسات الاجتماعية";
            t["life.subject.computing"] = "الحاسوب";
            t["life.subject.sports"] = "التربية الرياضية";

            t["life.trait.punctual"] = "الالتزام بالوقت";
            t["life.trait.punctual.info"] = "حضور في الفصل قبل الجرس دائمًا، حتى صار الجرس يشعر بالحرج.";
            t["life.trait.late"] = "كثرة التأخر";
            t["life.trait.late.info"] = "الزحام هو المتهم الدائم، حتى في الأيام الخالية من الزحام. بعض الحصص تبدأ بلا معلم.";
            t["life.trait.patient"] = "الصبر";
            t["life.trait.patient.info"] = "شرح الكسور للمرة التاسعة بالهدوء ذاته الذي كان في المرة الأولى.";
            t["life.trait.strict"] = "الحزم";
            t["life.trait.strict.info"] = "فصول صامتة وصفوف مستقيمة، وشكوى من البيوت بين حين وآخر.";
            t["life.trait.creative"] = "الإبداع";
            t["life.trait.creative.info"] = "دورة الماء بإبريق وأنشودة. وبين حين وآخر ترتفع المدرسة كلها بفكرة واحدة.";
            t["life.trait.ambitious"] = "الطموح";
            t["life.trait.ambitious.info"] = "سؤال مبكر عن راتب المدير. تعلّم سريع ورغبة في أن يلاحظ الجميع ذلك.";

            t["life.interview.reply.experience.punctual"] = "في أحد عشر عامًا لم أحتج يومًا إلى جرس يخبرني أين أكون.";
            t["life.interview.reply.experience.late"] = "ثلاث مدارس، كلها ممتازة. وأعتذر مجددًا عن هذا الصباح، فالدوّار كان فضيحة.";
            t["life.interview.reply.experience.patient"] = "قضيت فصلًا كاملًا مع طالب واحد والقسمة المطولة، وبلغنا الغاية في مايو.";
            t["life.interview.reply.experience.strict"] = "تنتهي فصولي من المنهج قبل موعده، ولم يسألني أحد قط عن سر هدوئها.";
            t["life.interview.reply.experience.creative"] = "درّست في ست قاعات، وغيّرت ترتيب الأثاث فيها جميعًا.";
            t["life.interview.reply.experience.ambitious"] = "أربع سنوات في التدريس وسنة في رئاسة القسم، وقد حان موعد الخطوة التالية.";
            t["life.interview.reply.discipline.punctual"] = "أقف عند الباب قبل وصولهم، ومن الصعب أن تتأخر على من ينتظرك.";
            t["life.interview.reply.discipline.late"] = "الأطفال يهدؤون وحدهم إذا منحتهم عشر دقائق قبل أن تصل إليهم.";
            t["life.interview.reply.discipline.patient"] = "أنتظر. لا أحد يستطيع مواصلة الغضب أمام من ينتظره بهدوء.";
            t["life.interview.reply.discipline.strict"] = "إنذار واحد، وقلّما احتجت إلى شرح ما يأتي بعده.";
            t["life.interview.reply.discipline.creative"] = "أعلى الطلاب صوتًا يدير التجربة، فإذا به أول من يطلب الصمت.";
            t["life.interview.reply.discipline.ambitious"] = "أسجل كل شيء، والنتائج تتحسن، ويسعدني أن أريك الرسم البياني.";
            t["life.interview.reply.dream.punctual"] = "جدول حصص يصمد أسبوعًا كاملًا. سأضعه في إطار.";
            t["life.interview.reply.dream.late"] = "بيت مجاور للمدرسة، وربما داخلها.";
            t["life.interview.reply.dream.patient"] = "أن ألقى طالبًا قديمًا ما زال يتذكر كيف يجمع الكسور.";
            t["life.interview.reply.dream.strict"] = "قاعة امتحان لا يطلب فيها أحد استعارة مسطرة.";
            t["life.interview.reply.dream.creative"] = "فصل بلا جدران، فإن تعذر ذلك فبنافذة كبيرة جدًا.";
            t["life.interview.reply.dream.ambitious"] = "كرسيك يا حضرة المدير، بعد خمس سنوات تقريبًا. مع كامل الاحترام.";

            t["life.case.fight"] = "شجار في الفناء بعد الفسحة، واسم {name} في وسطه. قميص ممزق وثلاث روايات للحادثة.";
            t["life.case.fight.punish"] = "إيقاف ليوم واحد";
            t["life.case.fight.talk"] = "اجلس واستمع";
            t["life.case.fight.parents"] = "استدعِ الأهل";
            t["life.case.fight.punish.ok"] = "يوم إيقاف في سجل {name}. هدأ الفناء، ولم يعد أحد إلى ذكر القميص.";
            t["life.case.fight.talk.ok"] = "استمعت إلى {name} على كوب ماء، فتبيّن أن للحادثة رواية رابعة.";
            t["life.case.fight.parents.ok"] = "حضرت عائلة {name} بأكملها، واعتذر الجميع للجميع، مطولًا.";
            t["life.case.phone"] = "رن هاتف في منتصف اختبار العلوم، وهو هاتف {name}. وكانت النغمة، للأسف، لا تُنسى.";
            t["life.case.phone.punish"] = "مصادرته أسبوعًا";
            t["life.case.phone.talk"] = "كلمة هادئة";
            t["life.case.phone.parents"] = "سلّمه إلى الأهل";
            t["life.case.phone.punish.ok"] = "يقيم هاتف {name} الآن في درج مكتبك، وتصله رسائل كثيرة.";
            t["life.case.phone.talk.ok"] = "حديث قصير مع {name}، وعاد الهاتف إلى الحقيبة صامتًا، حتى إشعار آخر.";
            t["life.case.phone.parents.ok"] = "عاد الهاتف إلى البيت عن طريق أهل {name}، ويقال إنه بقي هناك.";
            t["life.case.cheating"] = "لدى معلم الرياضيات ورقتان متطابقتان حتى في الخطأ نفسه، وإحداهما ورقة {name}.";
            t["life.case.cheating.punish"] = "صفر في الاختبار";
            t["life.case.cheating.talk"] = "اطلب الحقيقة";
            t["life.case.cheating.parents"] = "أبلغ الأهل";
            t["life.case.cheating.punish.ok"] = "صفر بخط واضح جدًا في ورقة {name}، ولقيت الورقة الأخرى المصير ذاته.";
            t["life.case.cheating.talk.ok"] = "بعد صمت طويل جاءت الحقيقة كاملة من {name}، واتُّفق على إعادة الاختبار.";
            t["life.case.cheating.parents.ok"] = "عرضت الورقتين على أهل {name}، ولم يحتج أحد إلى شرح الخطأ.";
            t["life.case.skipped"] = "في سجل الحضور غياب باسم {name}، وعند الحارس رواية أخرى: عند البقالة في العاشرة، وكوب كرك في اليد.";
            t["life.case.skipped.punish"] = "حجز طوال الأسبوع";
            t["life.case.skipped.talk"] = "اسأل عما يجري";
            t["life.case.skipped.parents"] = "اتصل بالبيت";
            t["life.case.skipped.punish.ok"] = "أسبوع كامل من الحجز في جدول {name}، ومعه اكتشاف أن في المكتبة ساعة لها صوت.";
            t["life.case.skipped.talk.ok"] = "كان وراء غياب {name} حصة بعينها، وأنت تعرف الآن أي حصة ولماذا.";
            t["life.case.skipped.parents.ok"] = "كانت المكالمة مع بيت {name} قصيرة، وكان الوصول في الصباح التالي مبكرًا جدًا.";
            t["life.case.rude"] = "رد جارح من {name} على معلم أمام الفصل كله، وما زالت غرفة المعلمين تناقش الصياغة.";
            t["life.case.rude.punish"] = "اعتذار مكتوب وحجز";
            t["life.case.rude.talk"] = "ناقش الأمر";
            t["life.case.rude.parents"] = "استدعِ الأهل";
            t["life.case.rude.punish.ok"] = "وصل اعتذار {name} مكتوبًا كاملًا، ولاحظ المعلم أن نحوه ممتاز.";
            t["life.case.rude.talk.ok"] = "سألت {name} عما حدث حقًا ذلك اليوم، فلم يكن للجواب علاقة كبيرة بالمعلم.";
            t["life.case.rude.parents.ok"] = "جلس أهل {name} في مكتبك ووافقوا المعلم، بصوت مرتفع.";
            t["life.case.late_again"] = "أربع مرات من التأخر في أسبوعين على اسم {name}، وأصبحت الأعذار مبتكرة إلى حد لافت.";
            t["life.case.late_again.punish"] = "بقاء بعد الجرس الأخير";
            t["life.case.late_again.talk"] = "اسأل عن الصباح";
            t["life.case.late_again.parents"] = "كلّم الأهل";
            t["life.case.late_again.punish.ok"] = "صار بقاء {name} بعد الدوام عادة، وكل دقيقة تُعوَّض تحت نظر الحارس.";
            t["life.case.late_again.talk.ok"] = "هناك إخوة صغار لا بد من إيصالهم إلى مدرسة أخرى. واتُّفق مع {name} على موعد جديد.";
            t["life.case.late_again.parents.ok"] = "وعد أهل {name} بشراء منبّه، فوصل منبّهان.";
            t["life.case.vandal"] = "حفر أحدهم اسمًا على طاولة جديدة بالفرجار، والاسم هو {name}، وهذا وفّر علينا التحقيق.";
            t["life.case.vandal.punish"] = "دفع ثمن الطاولة";
            t["life.case.vandal.talk"] = "صنفرتها معًا";
            t["life.case.vandal.parents"] = "أرِ الأهل الطاولة";
            t["life.case.vandal.punish.ok"] = "جاء ثمن الطاولة من مصروف {name} لشهر كامل، بالتقسيط.";
            t["life.case.vandal.talk.ok"] = "قضيت مع {name} عصرًا بورق الصنفرة. صلحت الطاولة، وكان الحديث أفضل منها.";
            t["life.case.vandal.parents.ok"] = "تأمل أهل {name} الطاولة طويلًا، ثم عرضوا شراءها.";
            t["life.case.bullied"] = "وصلت إلى مكتبك ورقة: مكان {name} وقت الفسحة صار على الدرج، بعيدًا عن الجميع، وبعض الزملاء حريصون على بقاء الحال كذلك.";
            t["life.case.bullied.punish"] = "عاقب المسؤولين";
            t["life.case.bullied.talk"] = "اجلس مع الطالب";
            t["life.case.bullied.parents"] = "استدعِ العائلتين";
            t["life.case.bullied.punish.ok"] = "خسر المتزعمون فسحة شهر كامل، وفي اليوم التالي كانت فسحة {name} في الفناء، بحذر.";
            t["life.case.bullied.talk.ok"] = "جلست مع {name} على الدرج وقت الفسحة. لم تقل كثيرًا، وبدا أن هذا نافع.";
            t["life.case.bullied.parents.ok"] = "اجتمعت عائلتان في مكتبك من أجل {name}. كان لقاءً محرجًا، وكان لا بد منه.";
            t["life.case.followup.punish.good"] = "بعد أسبوع لا جديد عن {name} في دفتر المخالفات. يبدو أن الدرس رسخ.";
            t["life.case.followup.punish.bad"] = "بعد أسبوع عاد اسم {name} إلى دفتر المخالفات بشكوى جديدة، ولم يبلغ العقاب مداه.";
            t["life.case.followup.talk.good"] = "بعد أسبوع جاءت تحية من {name} عند بابك، صباح الخير لا أكثر.";
            t["life.case.followup.talk.bad"] = "بعد أسبوع ما زالت إيماءات {name} مهذبة ولم يتغير شيء. كان الكلام هو الجزء السهل.";
            t["life.case.followup.parents.good"] = "بعد أسبوع صارت واجبات {name} تصل موقعة وفي موعدها. هناك من يتابع في البيت.";
            t["life.case.followup.parents.bad"] = "بعد أسبوع توتر الجو في بيت {name}، ووصل التوتر إلى المدرسة.";

            t["life.action.title"] = "يوم المدير";
            t["life.action.gate"] = "صباح عند البوابة مع الأهالي";
            t["life.action.broadcast_kindness"] = "إذاعة الصباح عن الرفق";
            t["life.action.broadcast_study"] = "إذاعة الصباح عن الاجتهاد";
            t["life.action.broadcast_discipline"] = "إذاعة الصباح عن الانضباط";
            t["life.action.observe"] = "جلست في آخر الفصل تتابع حصة {name}، ودفترك مغلق.";

            t["life.up.ac"] = "تكييف جديد";
            t["life.up.ac.info"] = "المبنى البارد يُغفر له الكثير. يخفف شكوى واحدة من الأهالي كل أسبوع.";
            t["life.up.ac.done"] = "اشتغل التكييف الجديد. لم يذكر أحد الحر، وهذا أعلى مديح متاح.";
            t["life.up.canteen"] = "مقصف لائق";
            t["life.up.canteen.info"] = "طعام ساخن بسعر معقول، ودخل ثابت كل أسبوع.";
            t["life.up.canteen.done"] = "افتُتح المقصف، وانتهت أزمة السمبوسة رسميًا.";
            t["life.up.library"] = "مكتبة";
            t["life.up.library.info"] = "رفوف وهدوء وارتفاع بطيء ومضمون في المستوى الدراسي.";
            t["life.up.library.done"] = "فتحت المكتبة أبوابها، وأُعير أول كتاب بعد أربع دقائق.";
            t["life.up.lab"] = "مختبر العلوم";
            t["life.up.lab.info"] = "تجارب حقيقية، ومستوى دراسي يتحسن باطراد.";
            t["life.up.lab.done"] = "المختبر جاهز. يُرجى الانتباه إلى مكان بطانية الحريق.";
            t["life.up.playground"] = "ملعب مظلل";
            t["life.up.playground.info"] = "يعود الأطفال إلى بيوتهم متعبين سعداء، ويلاحظ الأهالي ذلك.";
            t["life.up.playground.done"] = "اكتمل الملعب. صارت الفسحة أقصر في نظر الجميع إلا المعلمين.";
            t["life.up.garden"] = "حديقة أمامية";
            t["life.up.garden.info"] = "يبدأ المارة في الحديث عن المدرسة بنبرة ألطف.";
            t["life.up.garden.done"] = "زُرعت الحديقة، وأخبرك ثلاثة معلمين بذلك حتى الآن.";
            t["life.up.bus"] = "حافلات مدرسية";
            t["life.up.bus.info"] = "صار بوسع الأسر البعيدة أن تفكر فيك. يزداد المتقدمون.";
            t["life.up.bus.done"] = "وصلت الحافلات بطلاء جديد، وللسائقين آراء جاهزة في المواقف.";
            t["life.up.training_center"] = "مركز التدريب";
            t["life.up.training_center.info"] = "أرسل المعلمين إلى الدورات هنا داخل المدرسة. مقعدان.";
            t["life.up.training_center.done"] = "افتُتح مركز التدريب. السبورة جديدة، والأقلام ما زالت تكتب.";
            t["life.up.training_center2"] = "توسعة مركز التدريب";
            t["life.up.training_center2.info"] = "مقعدان إضافيان، حتى لا تخلو غرفة المعلمين دفعة واحدة.";
            t["life.up.training_center2.done"] = "التوسعة جاهزة، وصار بالإمكان تحسين أربعة معلمين في وقت واحد.";
            t["life.up.wing"] = "جناح جديد";
            t["life.up.wing.info"] = "جناح كامل، وستون مقعدًا إضافيًا، وغبار كثير.";
            t["life.up.wing.done"] = "اكتمل الجناح الجديد. ستون كرسيًا فارغًا في الانتظار، والطلاء يكاد يجف.";

            t["life.course.method"] = "طرق التدريس";
            t["life.course.method.info"] = "تجديد متين، ويعود المعلم بعادات جديدة.";
            t["life.course.method.done"] = "انتهت دورة طرق التدريس، و{name} في حصص {subject} من جديد بأفكار جديدة.";
            t["life.course.subject"] = "التمكن من المادة";
            t["life.course.subject.info"] = "دراسة معمقة في مادة المعلم. غياب طويل ومكسب كبير.";
            t["life.course.subject.done"] = "انتهت دورة التمكن من المادة، وصار عند {name} في {subject} ما ليس في الكتاب المقرر.";
            t["life.course.master"] = "برنامج المعلم الخبير";
            t["life.course.master.info"] = "أصعب الدورات وأفضلها نتيجة، ويعقبها إرهاق غير قليل.";
            t["life.course.master.done"] = "انتهى برنامج الخبراء، وعاد اسم {name} إلى جدول {subject}، ومعه إرهاق وهيبة.";
            t["life.course.care"] = "راحة المعلمين";
            t["life.course.care.info"] = "قصيرة ورخيصة ومريحة، لا تعلّم شيئًا وتصلح الكثير.";
            t["life.course.care.done"] = "انتهت دورة الراحة، وظهرت ابتسامة {name} في حصص {subject}، فأقلقت الطلاب.";

            t["life.school.0"] = "مدرسة الفنار";
            t["life.school.1"] = "مدرسة السدرة";
            t["life.school.2"] = "مدرسة البدع";
            t["life.school.3"] = "مدرسة المها";
            t["life.school.4"] = "مدرسة الدانة";
            t["life.school.5"] = "مدرسة الغاف";
            t["life.school.6"] = "مدرسة الشمال";
            t["life.school.7"] = "مدرسة بوابة الريان";

            t["life.crest.0"] = "صقر";
            t["life.crest.1"] = "نخلة";
            t["life.crest.2"] = "سفينة";
            t["life.crest.3"] = "مصباح";
            t["life.crest.4"] = "هلال";
            t["life.crest.5"] = "كتاب";

            t["life.phil.strict"] = "الجدية أولًا";
            t["life.phil.strict.info"] = "معايير عالية ونتائج طيبة، وقد يراها بعض الأهالي مبالغة.";
            t["life.phil.creative"] = "الفضول أولًا";
            t["life.phil.creative.info"] = "يحب المعلمون هذه الحرية، وتدفع درجات الامتحان ثمنها أحيانًا.";
            t["life.phil.sports"] = "العقل السليم والجسم السليم";
            t["life.phil.sports.info"] = "يحب الأهالي مدرسة لها فريق، وقد يخسر وقت الدروس شيئًا.";
            t["life.phil.faith"] = "الإيمان والأخلاق";
            t["life.phil.faith.info"] = "أدب وقيم وثقة راسخة من الأسر.";

            t["life.act.summer_camp"] = "أقم مخيمًا صيفيًا، {cash} ريال";
            t["life.act.summer_camp.ok"] = "امتلأت كل القاعات بالمخيم، والمعلمون الذين تمنوا صيفًا هادئًا لا يكلمونك.";
            t["life.act.adult_courses"] = "استضف دورات مسائية للكبار، {cash} ريال";
            t["life.act.adult_courses.ok"] = "تكدّس رجال كبار على طاولات صغيرة ليتعلموا جداول البيانات، ودفعوا في الموعد.";

            t["life.loan.button"] = "خذ قرضًا مصرفيًا";
            t["life.loan.taken"] = "وافق المصرف بسرعة، وهذا ما أقلقك. تُخصم الأقساط الآن كل أسبوع.";
            t["life.loan.paid"] = "سُدد آخر قسط. أرسل المصرف بطاقة تهنئة، ولم تضعها في إطار.";

            t["life.rival.30"] = "افتُتحت أكاديمية جديدة على بعد شارعين، ولافتتها أطول من مبناك. والأهالي بدأوا يسألون.";
            t["life.rival.30.scholarship"] = "اعرض منحًا دراسية";
            t["life.rival.30.open_day"] = "أقم يومًا مفتوحًا";
            t["life.rival.30.ignore"] = "لا تلتفت إليها";
            t["life.rival.30.scholarship.ok"] = "صارت منحك حديث الحي أسبوعًا كاملًا، وبدت اللافتة أصغر قليلًا.";
            t["life.rival.30.open_day.ok"] = "تجولت الأسر في فصولك، وأكلت من بسكويتك، وبقيت تتحدث.";
            t["life.rival.30.ignore.ok"] = "انشغلت بمدرستك، وبقيت اللافتة في مكانها.";
            t["life.rival.60"] = "تعرض الأكاديمية الآن أجهزة لوحية مجانية وتطبيقًا للأهالي، وبعض أسرك صارت تتجنب نظرتك عند البوابة.";
            t["life.rival.60.scholarship"] = "موّل مزيدًا من المنح";
            t["life.rival.60.open_day"] = "افتح الأبواب من جديد";
            t["life.rival.60.ignore"] = "ثق بسمعتك";
            t["life.rival.60.scholarship.ok"] = "كانت الكلفة مؤلمة، لكن أسرًا كانت قد سحبت ملفاتها أعادتها.";
            t["life.rival.60.open_day.ok"] = "عاد طلابك القدامى ليتحدثوا، وهذا ما لم يفعله أي جهاز لوحي بعد.";
            t["life.rival.60.ignore.ok"] = "صمتّ وواصلت عملك، وخلت بضع طاولات أخرى في آخر الفصول.";

            t["life.season.exams_mid"] = "بدأت امتحانات منتصف العام، وصارت الممرات مرتبة على نحو مريب.";
            t["life.season.exams_mid.banner"] = "امتحانات منتصف العام";
            t["life.season.ramadan"] = "أهلّ رمضان. يوم أقصر وأصوات أخفض ومقصف بلا عمل.";
            t["life.season.ramadan.banner"] = "رمضان كريم";
            t["life.season.national"] = "اليوم الوطني. أعلام على كل نافذة، ومسيرة كادت تنتظم خطواتها.";
            t["life.season.national.banner"] = "اليوم الوطني";
            t["life.season.exams_final"] = "الامتحانات النهائية. لا أحد يستعير مسطرة إلا لسبب وجيه جدًا.";
            t["life.season.exams_final.banner"] = "الامتحانات النهائية";

            t["life.ministry"] = "مفتش من الوزارة عند البوابة، معه ملف سميك وبلا موعد مسبق. أمامك عشر دقائق.";
            t["life.ministry.show"] = "اعرض أفضل الفصول";
            t["life.ministry.show.ok"] = "رأى المفتش أفضل دروسك، وكتب طويلًا وهو يومئ برأسه.";
            t["life.ministry.show.fail"] = "فتح المفتش بابًا لم تختره. كانت ملاحظته قصيرة، وتحتها خط.";
            t["life.ministry.prepare"] = "سلّم السجلات";
            t["life.ministry.prepare.ok"] = "كانت الملفات كلها سليمة. بدا المفتش محبطًا بعض الشيء، ثم وقّع.";
            t["life.ministry.prepare.fail"] = "كانت السجلات مرتبة، ولم تكن النتائج التي فيها كذلك. قلّب المفتش الصفحات ببطء.";
            t["life.ministry.honest"] = "قل الحقيقة كاملة";
            t["life.ministry.honest.ok"] = "ذكرت نقاط ضعفك قبل أن تُسأل، فأغلق المفتش الملف وطلب شايًا.";
            t["life.ministry.honest.fail"] = "كنت صادقًا، وشكرك المفتش، وكتب كل ما قلته.";

            t["life.grad.body"] = "آخر أمسية في العام. مقاعد مصفوفة وأثواب تخرج أطول قليلًا مما ينبغي، وكل ولي أمر يرفع هاتفه. الجميع ينتظر كلمتك.";
            t["life.grad.thanks"] = "اشكر المعلمين";
            t["life.grad.thanks.ok"] = "قرأت اسم كل معلم فوقفت القاعة، وتظاهر بعضهم بأن شيئًا دخل عينه.";
            t["life.grad.dreams"] = "حدّثهم عن مستقبلهم";
            t["life.grad.dreams.ok"] = "قلت لهم إن العالم أصغر مما يبدو وإنه لهم. كتب بعض الأهالي ذلك في هواتفهم.";
            t["life.grad.duty"] = "حدّثهم عن الواجب";
            t["life.grad.duty.ok"] = "طلبت منهم أن يعودوا يومًا ويتركوا هذه المدينة أفضل مما وجدوها. سكتت القاعة، وطال سكوتها.";

            t["life.rank"] = "الترتيب في المدينة: {rank} من 10";

            t["life.goal.title"] = "هدف اليوم";
            t["life.goal.no_low_mood"] = "حافظ على مزاج طيب لكل المعلمين";
            t["life.goal.reach_parents"] = "اكسب الأهالي إلى صفك";
            t["life.goal.earn_tonight"] = "اكسب شيئًا الليلة";
            t["life.goal.visit_gate"] = "استقبل الأهالي عند البوابة";
            t["life.goal.observe_teacher"] = "احضر حصة أحد المعلمين";
            t["life.goal.done"] = "تحقق الهدف، ووجدت مكافأة صغيرة طريقها إلى الحسابات.";

            t["life.ach.title"] = "الإنجازات";
            t["life.ach.no_dismissals"] = "لم يغادر أحد";
            t["life.ach.no_dismissals.info"] = "أنهِ العام دون فصل أي معلم.";
            t["life.ach.full_house"] = "لا مقعد شاغرًا";
            t["life.ach.full_house.info"] = "املأ كل مقاعد المدرسة.";
            t["life.ach.first_upgrade"] = "أعمال بناء";
            t["life.ach.first_upgrade.info"] = "أكمل أول مشروع بناء.";
            t["life.ach.trained_five"] = "كلية المعلمين";
            t["life.ach.trained_five.info"] = "أرسل خمسة معلمين إلى الدورات.";
            t["life.ach.rank_one"] = "الأولى في المدينة";
            t["life.ach.rank_one.info"] = "ابلغ المركز الأول في ترتيب المدينة.";
            t["life.ach.streak_seven"] = "سبعة صباحات";
            t["life.ach.streak_seven.info"] = "اقرأ رسالة الصباح سبعة أيام متتالية.";

            t["life.memories.title"] = "الذكريات";

            t["life.tab.school"] = "المدرسة";
            t["life.tab.people"] = "الناس";
            t["life.tab.build"] = "البناء";
            t["life.tab.train"] = "التدريب";
            t["life.tab.office"] = "المكتب";

            t["life.people.title"] = "طاقم المدرسة";
            t["life.people.hire"] = "قابل مرشحين";
            t["life.people.dismiss"] = "إنهاء الخدمة";
            t["life.people.observe"] = "حضور حصة";
            t["life.people.skill"] = "المهارة";
            t["life.people.mood"] = "المزاج";
            t["life.people.pay"] = "الأجر الأسبوعي";
            t["life.people.trait.unknown"] = "غير معروفة بعد";
            t["life.people.in_training"] = "في دورة";

            t["life.interview.title"] = "المقابلات";
            t["life.interview.ask.experience"] = "حدثني عن خبرتك.";
            t["life.interview.ask.discipline"] = "كيف تتعامل مع فصل صعب؟";
            t["life.interview.ask.dream"] = "بماذا تحلم؟";
            t["life.interview.hire"] = "تعيين";
            t["life.interview.cancel"] = "اصرف الجميع";
            t["life.interview.band.low"] = "خبرة قليلة على ما يبدو";
            t["life.interview.band.mid"] = "كفاءة جيدة على ما يبدو";
            t["life.interview.band.high"] = "كفاءة ممتازة على ما يبدو";

            t["life.build.title"] = "مشروعات البناء";
            t["life.build.cost"] = "الكلفة";
            t["life.build.time"] = "المدة";
            t["life.build.building"] = "قيد الإنشاء";
            t["life.build.done"] = "مكتمل";
            t["life.build.start"] = "ابدأ العمل";

            t["life.train.title"] = "مركز التدريب";
            t["life.train.slots"] = "المقاعد";
            t["life.train.start"] = "أرسله إلى الدورة";
            t["life.train.need_center"] = "ابنِ مركز التدريب أولًا.";
            t["life.train.remaining"] = "الوقت المتبقي";

            t["life.time.remaining"] = "باقي {hours} س {minutes} د";
            t["life.time.duration"] = "{hours} س {minutes} د";
            t["life.clock.extend"] = "مدّد اليوم";
            t["life.clock.shorten"] = "قصّر اليوم";

            t["life.identity.title"] = "هوية مدرستك";
            t["life.identity.name"] = "اسم المدرسة";
            t["life.identity.crest"] = "الشعار";
            t["life.identity.colour"] = "اللون";
            t["life.identity.philosophy"] = "الفلسفة";

            t["life.form.period_minutes"] = "دقائق الحصة";
            t["life.form.break_minutes"] = "دقائق الفسحة";

            t["life.students.title"] = "طلاب تتابعهم";
            t["life.students.marks"] = "الدرجات";
            t["life.students.conduct"] = "السلوك";

            t["rule.LifeTeacherBusy"] = "هذا المعلم في دورة حاليًا.";
            t["rule.LifeNoCandidate"] = "هذا المرشح غير متاح.";
            t["rule.LifeUpgradeBusy"] = "هناك مشروع بناء جارٍ بالفعل.";
            t["rule.LifeUpgradeDone"] = "بُني هذا من قبل.";
            t["rule.LifeUpgradeLocked"] = "يجب إنجاز مشروع سابق أولًا.";
            t["rule.LifeNoTrainingSlot"] = "لا توجد مقاعد شاغرة في الدورات.";
            t["rule.LifeNeedsTrainingCenter"] = "يتطلب هذا مركز تدريب.";
            t["rule.LifeLoanActive"] = "ما زلت تسدد قرضًا.";
            t["rule.LifeActionUsed"] = "فعلت هذا اليوم بالفعل.";
            t["rule.LifeCasePending"] = "انظر أولًا في القضية التي على مكتبك.";
            t["rule.LifeLastTeacher"] = "لا يمكن أن تعمل المدرسة بلا معلم.";
            t["rule.LifeDayLimit"] = "لا يمكن تغيير اليوم الدراسي أكثر من ذلك.";
        }
    }
}
