namespace MyThreadPool
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// Класс пула потоков
    /// </summary>
    public class ThreadPool
    {
        /// <summary>
        /// Класс задач, принимающихся потоками на исполнение
        /// </summary>
        /// <typeparam name="TResult">Тип результата задач</typeparam>
        private class MyTask<TResult> : IMyTask<TResult>
        {
            /// <summary>
            /// Конструктор класса задач, принимающихся на исполнение
            /// </summary>
            /// <param name="calculation">Вычисление, представляющее задачу</param>
            /// <param name="threadpool">Экземпляр класса пула потоков</param>
            public MyTask(Func<TResult> calculation, ThreadPool threadPool, TaskInfo previousTaskInfo)
            {
                this.calculation = calculation;
                this.threadPool = threadPool;                
                this.previousTaskInfo = previousTaskInfo;                
            }

            /// <summary>
            /// Объект класса, хранящего значения - выполнена ли задача (для информации 
            /// о предыдущих задачах в случае использования продолжения)
            /// </summary>
            private TaskInfo previousTaskInfo;

            /// <summary>
            /// Вычисление, предсталвяющее задачу
            /// </summary>
            private Func<TResult> calculation;

            /// <summary>
            /// Исключение, вызванное работой над задачей
            /// </summary>
            private Exception taskException;

            /// <summary>
            /// Результат выполнения задачи
            /// </summary>
            private TResult resultOfTask;

            /// <summary>
            /// Объект для ожидания выполнения задачи каким-либо потоком
            /// </summary>
            private ManualResetEvent waitResult = new ManualResetEvent(false);

            /// <summary>
            /// Экземпляр пула потоков
            /// </summary>
            private ThreadPool threadPool;

            /// <summary>
            /// Синхронизация метода ContinueWith
            /// </summary>
            private object lockerContinueWith = new object();

            /// <summary>
            /// Поле с информацией: выполнена ли задача, которую хотим продолжить.
            /// </summary>
            public bool isCompleted;

            /// <summary>
            /// Возвращает true, если задача выполнена.
            /// </summary>
            public bool IsCompleted
            {
                get => isCompleted;
                private set
                {
                    isCompleted = value;
                }
            }

            /// <summary>
            /// Возвращает результат выполнения задачи.
            /// Если ещё не вычислено, то ждёт и блокирует вызвавший поток.
            /// </summary>
            public TResult Result
            {
                get
                {                                               
                    waitResult.WaitOne();

                    if (taskException != null)
                    {
                        throw new AggregateException("taskException", taskException);
                    }

                    return resultOfTask;
                }
            }       
            
            /// <summary>
            /// Метод, проводящий вычисление задачи
            /// </summary>
            internal void ExecuteTask()
            {
                try
                {
                    resultOfTask = calculation();
                    previousTaskInfo.PreviousTaskIsCompleted = true;
                    Volatile.Write(ref calculation, null);                    
                    IsCompleted = true;                    

                }
                catch (Exception e)
                {
                    taskException = e;                  
                }    
                finally
                {
                    waitResult.Set();
                }
            }

            /// <summary>
            /// Создание продолжения задачи
            /// </summary>
            /// <typeparam name="TNewResult">Результат новой задачи</typeparam>T
            /// <param name="supplier">Вычисление для новой задачи</param>
            /// <returns>Задачу, поставленную на исполнение</returns>
            public IMyTask<TNewResult> ContinueWith<TNewResult>(Func<TResult, TNewResult> supplier)
            {                               
                return threadPool.AddTask(() => supplier(Result), previousTaskInfo, new TaskInfo());
                // --------------------------------------------------------------------------/\----------------
                // ------------------------------------------------------для возможности продолжать продолжения 
            }
        }                
        
        /// <summary>
        /// Потоки для задач
        /// </summary>
        private Thread[] threads;

        /// <summary>
        /// Объект для коллаборативной отмены
        /// </summary>
        private CancellationTokenSource source = new CancellationTokenSource();        

        /// <summary>
        /// Объект для переноса потока в режим ожидания
        /// </summary>
        private ManualResetEvent waitingForTask = new ManualResetEvent(false);

        /// <summary>
        /// Объект для ожидания остановки всех потоков
        /// </summary>
        private ManualResetEvent allThreadsStoped = new ManualResetEvent(false);

        /// <summary>
        /// Очередь задач для выполнения с объектом, предоставляющим информацию: 
        /// имеется ли результат задачи, которую хотим продолжить, и является ли задача продолжением. 
        /// 
        /// Потоки разбирают задачи и испоняют их.
        /// </summary>        
        private Queue<(Action, TaskInfo)> queueTasks = new Queue<(Action, TaskInfo)>();

        /// <summary>
        /// Количество работающих потоков
        /// </summary>
        private int countAliveThread = 0;

        /// <summary>
        /// Объект для контроля очереди
        /// </summary>
        private object lockerQueue = new object();

        /// <summary>
        /// Объект для контроля счетчика потоков
        /// </summary>
        private object lockerCountThreads = new object();

        /// <summary>
        /// Добавление задачи, которую свободные потоки принимают на исполнение. 
        /// </summary>
        /// <typeparam name="TResult">Тип возвращаемого результата задачи</typeparam>
        /// <param name="calculation">Вычисление, представляющее задачу</param>
        /// <returns>Новая задача</returns>
        public IMyTask<TResult> AddTask<TResult>(Func<TResult> calculation)
        {            
            return AddTask(calculation, null, new TaskInfo()); // для задач, которые не являются продолжением
        }

        /// <summary>
        /// Внутренняя работа с добавлением задачи для учёта и ContinueWith, и AddTask.
        /// </summary>
        /// <typeparam name="TResult">Тип возвращаемого результата задачи.</typeparam>
        /// <param name="calculation">Вычисление, представляющее задачу.</param>
        /// <param name="previousTaskInfo">Объект с информацией о предыдущей задаче.</param>
        /// <returns>Новая задача</returns>
        private IMyTask<TResult> AddTask<TResult>(Func<TResult> calculation, TaskInfo previousTaskInfo, TaskInfo currentTaskInfo)
        {
            if (source.IsCancellationRequested)
            {
                throw new AggregateException("Пул потоков завершает работу. Новые задачи больше не принимаются");
            }
            
            var newTask = new MyTask<TResult>(calculation, this, currentTaskInfo);
            lock (lockerQueue)
            {
                queueTasks.Enqueue((newTask.ExecuteTask, previousTaskInfo));
                waitingForTask.Set();
            }

            return newTask;
        }

        /// <summary>
        /// Конструктор, создающий фиксировано <see cref="numberMaxThread"/> потоков
        /// </summary>
        /// <param name="numberOfThreads">Количество потоков</param>
        public ThreadPool(int numberMaxThread)
        {            
            threads = new Thread[numberMaxThread];
            for (int i = 0; i < threads.Length; i++) 
            {
                threads[i] = new Thread(ToRunMethodWorkOfThread);
            }
            
            foreach (Thread thread in threads)
            {
                thread.Start();
            }            
        }       

        /// <summary>
        /// Метод работы потоков 
        /// </summary>
        private void ToRunMethodWorkOfThread()
        {
            lock (lockerCountThreads)
            {
                countAliveThread++;
            } 

            while (true)
            {
                if (source.IsCancellationRequested)
                {
                    break;
                }

                waitingForTask.WaitOne();

                Action task = null;

                lock (lockerQueue)
                {
                    if (queueTasks.Count == 0 || source.IsCancellationRequested)
                    {                        
                        continue;
                    }

                    var tupleActionAndBool = queueTasks.Dequeue();
                    // если это задача не продолжение
                    if (tupleActionAndBool.Item2 == null)
                    {
                        task = tupleActionAndBool.Item1;
                    }         
                    else
                    {
                        if (!tupleActionAndBool.Item2.PreviousTaskIsCompleted)
                        {
                            queueTasks.Enqueue((tupleActionAndBool.Item1, tupleActionAndBool.Item2));
                            continue;
                        }
                        else
                        {
                            task = tupleActionAndBool.Item1;
                        }
                    }
                    

                    if (queueTasks.Count == 0)
                    {
                        waitingForTask.Reset();
                    }                    
                }

                task.Invoke();
            }

            lock (lockerCountThreads)
            {
                countAliveThread--;
            }

            if (countAliveThread == 0)
            {
                allThreadsStoped.Set();
            }
        }                                   

        /// <summary>
        /// Осведомляет потоки о необходимости завершить работу
        /// </summary>
        public void Shutdown()
        {            
            source.Cancel();
            waitingForTask.Set();
            allThreadsStoped.WaitOne();
        }
        
        /// <summary>
        /// Возвращает количество работающих потоков
        /// </summary>
        /// <returns></returns>
        public int CountAliveThread() => countAliveThread;

        static void Main(string[] args)
        {
            
        }
    }    
}