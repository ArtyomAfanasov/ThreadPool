namespace MyThreadPool
{
    /// <summary>
    /// Класс, хранящий значение - выполнена ли задача и является ли задача продолжением (для информации 
    /// о предыдущих задачах в случае использования продолжения)
    /// </summary>
    class TaskInfo
    {
        /// <summary>
        /// Выполнена ли предыдущая задача?
        /// </summary>        
        public bool PreviousTaskIsCompleted { get; set; } = false;
    }
}