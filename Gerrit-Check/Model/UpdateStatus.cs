namespace Gerrit_Check.Model
{
    public class UpdateStatus
    {
        private bool _pendingReviewsComplete;
        private bool _submittableReviewsComplete;

        public bool HasNewPending { get; set; }

        public bool HasNewSubmittable { get; set; }

        public bool InProgress { get; set; }

        public bool PendingReviewsComplete
        {
            set { _pendingReviewsComplete = value; }
        }

        public bool SubmittableReviewsComplete
        {
            set { _submittableReviewsComplete = value; }
        }

        public bool UpdateComplete => _pendingReviewsComplete && _submittableReviewsComplete;
    }
}