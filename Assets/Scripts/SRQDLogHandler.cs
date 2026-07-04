using UnityEngine;

namespace SRQuestDownloader
{
    public abstract class SRLogHandler: MonoBehaviour {
        public abstract void DebugLog(string message);
        public abstract void ErrorLog(string message);
        public abstract void PersistLog(string message);
    }
}
