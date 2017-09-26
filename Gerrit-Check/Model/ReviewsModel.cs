﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json.Linq;

namespace Gerrit_Check.Model
{
    public class ReviewsModel
    {
        public ReviewsModel()
        {
            Server = string.Empty;
            Project = string.Empty;
            Username = string.Empty;
            _updateTimer = new Timer();
        }

        public delegate void UpdateEvent(UpdateStatus status);

        public event UpdateEvent OnUpdated;

        private readonly Timer _updateTimer;
        private const double UpdateTimerInterval = 300000;

        private readonly Dictionary<string, int> _pendingReviewToRevisions = new Dictionary<string, int>();

        private UpdateStatus _updateStatus;

        public int SubmittableReviews { get; set; }

        public int PendingReviews { get; set; }

        public string Server { get; set; }

        public string Project { get; set; }

        public string Username { get; set; }

        public void InitModel(string server, string project, string user)
        {
            PendingReviews = 0;
            SubmittableReviews = 0;
            Server = server;
            Project = project;
            Username = user;

            _updateTimer.AutoReset = false;
            _updateTimer.Interval = UpdateTimerInterval;
            _updateTimer.Elapsed += UpdateTimerOnElapsed;

            UpdateModel();
        }

        private void UpdateTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            UpdateModel();
        }

        private void UpdateModel()
        {
            if (Server == string.Empty || Project == string.Empty || Username == string.Empty)
                return;

            if (_updateStatus?.InProgress ?? false)
                return;

            var client = CreateGerritRequest();
            var pendingReviewsParameters = $"?q=status:open+project:{Project}+reviewer:{Username}&o=all_revisions";
            var submittableReviewsParameters = $"?q=status:open+project:{Project}+owner:{Username}";

            _updateStatus = new UpdateStatus {InProgress = true};

            client.GetAsync(pendingReviewsParameters).ContinueWith(ProcessPendingResult);
            client.GetAsync(submittableReviewsParameters).ContinueWith(ProcessSubmittableResult);
        }

        private void UpdateModelComplete()
        {
            _updateStatus.InProgress = false;
            OnUpdated?.Invoke(_updateStatus);
            _updateTimer.Start();
        }

        private void ProcessSubmittableResult(Task<HttpResponseMessage> task)
        {
            var response = task.Result;
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(response.ReasonPhrase);
                return;
            }
            var responseContent = response.Content.ReadAsStringAsync();
            responseContent.ContinueWith(contentTask =>
            {
                var content = contentTask.Result;
                var submittableCount = 0;
                var oldSubmittableCount = SubmittableReviews;
                content = CleanResult(content);
                try
                {
                    var reviews = JArray.Parse(content);
                    foreach (var review in reviews)
                    {
                        if (review["submittable"].Value<bool>())
                            submittableCount++;
                    }

                    if (oldSubmittableCount == submittableCount) return;

                    _updateStatus.HasNewSubmittable = true;
                    SubmittableReviews = submittableCount;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                finally
                {
                    _updateStatus.SubmittableReviewsComplete = true;
                    if (_updateStatus.UpdateComplete) UpdateModelComplete();
                }
            });
        }

        private void ProcessPendingResult(Task<HttpResponseMessage> task)
        {
            var response = task.Result;
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(response.ReasonPhrase);
                return;
            }
            var responseContent = response.Content.ReadAsStringAsync();
            responseContent.ContinueWith(contentTask =>
            {
                var oldPendingCount = PendingReviews;
                var newPendingCount = 0;
                var content = contentTask.Result;
                content = CleanResult(content);
                
                try
                {
                    var reviews = JArray.Parse(content);
                    foreach (var review in reviews)
                    {
                        if (review["submittable"].Value<bool>()) continue;

                        newPendingCount++;
                        var revisions = review["revisions"] as JArray;
                        var reviewNumber = review["_number"].ToString();
                        var oldRevisionCount = _pendingReviewToRevisions.ContainsKey(reviewNumber)
                            ? _pendingReviewToRevisions[reviewNumber]
                            : 0;
                        var newRevisionCount = revisions?.Count ?? 0;

                        if (oldRevisionCount == newRevisionCount) continue;

                        _pendingReviewToRevisions[reviewNumber] = newRevisionCount;
                        _updateStatus.HasNewPending = true;
                    }
                    _updateStatus.HasNewPending |= oldPendingCount < newPendingCount;
                    PendingReviews = newPendingCount;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                finally
                {
                    _updateStatus.PendingReviewsComplete = true;
                    if (_updateStatus.UpdateComplete) UpdateModelComplete();
                }
            });
        }

        /// <summary>
        /// Gerrit pads the beginning of the response content
        /// for security reasons. This removes it so we can
        /// parse the rest as JSON.
        /// </summary>
        /// <param name="content">The response content</param>
        /// <returns>Cleansed content</returns>
        private static string CleanResult(string content)
        {
            return content.Replace(")]}'", "");
        }

        private HttpClient CreateGerritRequest()
        {
            var client = new HttpClient();
            var baseAddress = $"{Server}/changes/";
            client.BaseAddress = new Uri(baseAddress);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }
    }
}