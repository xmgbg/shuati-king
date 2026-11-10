using ShuatiKing.Models;

namespace ShuatiKing.Services;

public sealed class LearningStore(string path)
{
    private readonly DatabaseStore database = new(SettingsStore.DatabaseForLearningPath(path));
    private bool ready;
    private void Prepare()
    {
        if (ready) return;
        database.Initialize(); database.InitializeLearning(); database.MigrateLearning(path); ready = true;
    }
    public LearningState Load() { Prepare(); return database.LoadLearning(); }
    public void Save(LearningState state) { Prepare(); database.SaveLearning(state); }
}
