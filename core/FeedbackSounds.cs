using System.IO;

namespace SpiderSurge.Common
{
    internal enum FeedbackCue : byte { Unlock, Purchase, Reroll, Confirm, Cancel, Challenge, Failure, Title }

    internal static class FeedbackSounds
    {
        private const string Channel = "surge.feedback";

        internal static void Initialize() => Net.RegisterFromHost(Channel, reader => Play((FeedbackCue)reader.ReadByte()));
        internal static void Shutdown() => Net.Unregister(Channel);
        internal static void Play(FeedbackCue cue)
        {
            if (SoundManager.Instance != null) SoundManager.Instance.PlayFeedback(cue);
        }

        internal static void Cancel(FeedbackCue cue)
        {
            if (SoundManager.Instance != null) SoundManager.Instance.CancelFeedback(cue);
        }

        internal static void Broadcast(FeedbackCue cue)
        {
            if (!Net.IsAuthority) return;
            Play(cue);
            Net.SendToAll(Channel, writer => writer.Write((byte)cue));
        }

        internal static (string name, bool queued) Clip(FeedbackCue cue)
        {
            switch (cue)
            {
                case FeedbackCue.Unlock: return ("feedback_unlock", true);
                case FeedbackCue.Purchase: return ("feedback_purchase", false);
                case FeedbackCue.Reroll:
                case FeedbackCue.Challenge: return ("feedback_transition", false);
                case FeedbackCue.Confirm: return ("feedback_confirm", false);
                case FeedbackCue.Cancel: return ("feedback_cancel", false);
                case FeedbackCue.Failure: return ("feedback_failure", true);
                case FeedbackCue.Title: return ("feedback_title", true);
                default: throw new InvalidDataException("Unknown feedback sound");
            }
        }
    }
}
