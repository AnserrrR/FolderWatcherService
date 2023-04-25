using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NCrontab;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace FolderWatcherService.src
{
    public class DirectoryWatcher : IHostedService, IDisposable
    {
        private readonly string _folderPath;
        private readonly string _cronExpression;
        private readonly ILogger<DirectoryWatcher> _logger;
        private readonly List<string> _createdFiles;
        private readonly List<string> _updatedFiles;
        private readonly List<string> _deletedFiles;
        private PhysicalFileProvider _watcher;
        private State _watcherState;

        public DirectoryWatcher(IConfiguration configuration, ILogger<DirectoryWatcher> logger)
        {
            _logger = logger;
            try
            {
                _folderPath = configuration.GetRequiredSection("FolderPath").Value
                    ?? Directory.GetCurrentDirectory();
                _cronExpression = configuration.GetRequiredSection("CronExpression").Value
                    ?? "* * * * *";
            }
            catch (InvalidOperationException e)
            {
                _logger.LogError($"Configuration parse error: {e.Message}");
                throw new Exception("Configuration parse error", e);
            }

            _createdFiles = new List<string>();
            _updatedFiles = new List<string>();
            _deletedFiles = new List<string>();
            _watcher = new PhysicalFileProvider(_folderPath);
            _watcherState = new State();
        }

        /// <summary>
        /// Start the service
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {

            while (true)
            {
                var cron = CrontabSchedule.Parse(_cronExpression);
                var now = DateTime.Now;
                var lastOccurrence = cron.GetNextOccurrences(now.Date, now.Date.AddDays(1)).Last();
                var nextOccurrence = cron.GetNextOccurrence(now);
                TimeSpan delay;
                IDisposable? disposable = null;

                // If the next occurrence is before the last occurrence, then we are at the end of the day
                if (lastOccurrence <= now)
                {
                    delay = now - nextOccurrence;

                    LogChangedFiles();
                    _logger.LogInformation($"Next launch at {nextOccurrence}");
                }
                // Otherwise, we are in the middle of the day
                else
                {
                    // Create a new watcher and token
                    _watcher = new PhysicalFileProvider(_folderPath);
                    _watcherState = GetState("");
                    var token = _watcher.Watch("**/*.*");

                    // We only want to invoke the callback once per change, so we need to track if it has been invoked
                    var isCallbackInvoked = false;
                    disposable = ChangeToken.OnChange(() => token, () =>
                    {
                        if (!isCallbackInvoked)
                        {
                            OnFileChanged();
                            isCallbackInvoked = true;
                        }
                        else
                        {
                            isCallbackInvoked = false;
                        }
                    });

                    _logger.LogInformation("Starting directory watcher");

                    delay = lastOccurrence - now;
                }

                await Task.Delay(delay);

                disposable?.Dispose();
            }
        }

        /// <summary>
        /// On file changed event handler
        /// </summary>
        private void OnFileChanged()
        {
            // Get the new state of the folder
            var newState = GetState("");

            // Compare the old and new states to see what changed
            var changes = GetChanges(_watcherState, newState);

            // Update the state to the new state
            _watcherState = newState;

            // Log and store the changes
            if(changes.Created.Count > 0)
            {
                _logger.LogInformation($"Created: {string.Join(",\n\t", changes.Created)}");
                _createdFiles.AddRange(changes.Created);
            }
            if(changes.Changed.Count > 0)
            {
                _logger.LogInformation($"Changed: {string.Join(",\n\t", changes.Changed)}");
                _updatedFiles.AddRange(changes.Changed);
            }
            if(changes.Deleted.Count > 0)
            {
                _logger.LogInformation($"Deleted: {string.Join(",\n\t", changes.Deleted)}");
                _deletedFiles.AddRange(changes.Deleted);
            }
        }

        /// <summary>
        /// Stop the service
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            LogChangedFiles();
            _logger.LogInformation("Stopping directory watcher");

            return Task.CompletedTask;
        }

        /// <summary>
        /// Get the state of the folder
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private State GetState(string path)
        {
            var state = new State();

            // Get the files in the current directory
            foreach (var fileInfo in _watcher.GetDirectoryContents(path))
            {
                // If the file is a directory, get the state of that directory
                if(fileInfo.IsDirectory)
                {
                    state.Files = state.Files
                        .Union(GetState(Path.Combine(path, fileInfo.Name)).Files)
                        .ToDictionary(k => k.Key, v => v.Value);
                    continue;
                }

                state.Files[fileInfo.Name] = fileInfo.LastModified;
            }

            return state;
        }

        /// <summary>
        /// Get changes in the state of the folder
        /// </summary>
        /// <param name="oldState"></param>
        /// <param name="newState"></param>
        /// <returns></returns>
        private Changes GetChanges(State oldState, State newState)
        {
            var changes = new Changes();

            // Compare the old and new states to see what changed
            foreach (var file in oldState.Files)
            {
                if (!newState.Files.ContainsKey(file.Key))
                {
                    changes.Deleted.Add(file.Key);
                }
                else if (newState.Files[file.Key] != file.Value)
                {
                    changes.Changed.Add(file.Key);
                }
            }

            // Compare the new and old states to see what created
            foreach (var file in newState.Files)
            {
                if (!oldState.Files.ContainsKey(file.Key))
                {
                    changes.Created.Add(file.Key);
                }
            }

            return changes;
        }

        /// <summary>
        /// Logging and clearing the list of changed files
        /// </summary>
        private void LogChangedFiles()
        {
            if (_createdFiles.Count > 0)
            {
                _logger.LogInformation($"Created files:");
                foreach (var file in _createdFiles)
                {
                    _logger.LogInformation($" - {file}");
                }
            }

            if (_updatedFiles.Count > 0)
            {
                _logger.LogInformation($"Updated files:");
                foreach (var file in _updatedFiles)
                {
                    _logger.LogInformation($" - {file}");
                }
            }

            if (_deletedFiles.Count > 0)
            {
                _logger.LogInformation($"Deleted files:");
                foreach (var file in _deletedFiles)
                {
                    _logger.LogInformation($" - {file}");
                }
            }

            _createdFiles.Clear();
            _updatedFiles.Clear();
            _deletedFiles.Clear();
        }

        public void Dispose()
        {
            _watcher.Dispose();
        }
    }
}
