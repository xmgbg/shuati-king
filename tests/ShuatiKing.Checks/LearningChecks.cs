using System.IO;
using ShuatiKing.Models;
using ShuatiKing.Services;

internal static class LearningChecks
{
    internal static void Models()
    {
        var bank = new SampleLibrary("判分测试", "test", 1, 1, 1, 1, true);
        var state = new LearningState();
        var single = new AnswerItem { Question = LearningRules.Snapshot(bank.Questions[0]), Selected = [1] };
        Program.Check(LearningRules.IsCorrect(single), "单选正确判分");
        var multiple = new AnswerItem { Question = LearningRules.Snapshot(bank.Questions[1]), Selected = [0] };
        Program.Check(!LearningRules.IsCorrect(multiple), "多选少选不得分");
        multiple.Selected = [2, 0]; Program.Check(LearningRules.IsCorrect(multiple), "多选顺序不影响判分");
        multiple.Selected = [0, 1, 2]; Program.Check(!LearningRules.IsCorrect(multiple), "多选错选不得分");
        var blank = new AnswerItem { Question = LearningRules.Snapshot(bank.Questions[3]), Blanks = ["  TRANSMISSION CONTROL PROTOCOL ", "用户数据报协议"] };
        Program.Check(LearningRules.IsCorrect(blank), "填空接受别名并忽略英文大小写和首尾空白");
        blank.Blanks[1] = "错误答案"; Program.Check(!LearningRules.IsCorrect(blank), "多空必须全部匹配");
        single.Selected = [0]; LearningRules.Record(state, bank.Id, bank.Name, single); LearningRules.Record(state, bank.Id, bank.Name, single);
        Program.Check(state.Wrongs.Count == 1, "同一错题不会重复加入");
        single.Selected = [1]; Program.Check(LearningRules.Record(state, bank.Id, bank.Name, single) && state.Wrongs.Count == 0, "答对一次自动移出错题本");
        Program.Check(LearningRules.Shortage(bank, [2, 0, 0, 0])!.Contains("单选题缺 1"), "题量不足定位题型与数量");
        var exam = LearningRules.CreateExam(bank, [1, 1, 1, 1], [2, 3, 4, 5], 1);
        Program.Check(exam.Items.Select(i => i.Question.Id).Distinct().Count() == 4 && exam.Total == 14, "组卷不重复且按题型计算总分");
        bank.Questions[0].Stem = "修改原题";
        Program.Check(exam.Items.All(i => i.Question.Stem != "修改原题"), "试卷快照不受原题修改影响");
        exam.Advance(5); exam.Paused = true; exam.Advance(20);
        Program.Check(exam.RemainingSeconds == 55, "暂停期间不扣时");
        state.Exam = exam;
        var path = Path.GetFullPath($".artifacts/learning-check-{Guid.NewGuid()}.json");
        var store = new LearningStore(path); store.Save(state);
        var restored = store.Load(); File.Delete(path);
        Program.Check(restored.Exam!.Paused && restored.Exam.RemainingSeconds == 55 && restored.Exam.Items[0].Question.Id == exam.Items[0].Question.Id, "恢复作答快照与剩余时间并保持暂停");
        restored.Exam.Paused = false; restored.Exam.Advance(100); LearningRules.FinishExam(restored, restored.Exam, true);
        Program.Check(restored.Exam.Finished && restored.Exam.Score == 0 && restored.Wrongs.Count == 4, "到时交卷未作答零分并加入错题");
        LearningRules.FinishExam(restored, restored.Exam, true);
        Program.Check(restored.Wrongs.Count == 4, "交卷幂等不重复记录");
        var ordered = LearningRules.CreateExam(bank, [1, 0, 0, 1], [125, 1, 1, 50], 1, [QuestionType.Blank, QuestionType.Single]);
        Program.Check(ordered.Items.Select(i => i.Question.Type).SequenceEqual(new[] { QuestionType.Blank, QuestionType.Single }), "试卷遵循指定题型顺序");
        Program.Check(ordered.Total == 175, "满分可大于100且不做归一化");
        var lowTotal = LearningRules.CreateExam(bank, [0, 0, 1, 0], [1, 1, 7.5m, 1], 1, [QuestionType.Judgment]);
        Program.Check(lowTotal.Total == 7.5m && lowTotal.Items.Count == 1, "可只选一种题型并使用低于100的总分");
        var one = SampleLibrary.CreateExamples()[0]; var two = SampleLibrary.CreateExamples()[0];
        Program.Check(one.Id == two.Id && one.Questions[0].Id == two.Questions[0].Id, "示例题稳定标识支持重开后的错题关联");
    }
}
