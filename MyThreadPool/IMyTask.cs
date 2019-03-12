namespace MyThreadPool
{
    using System;

    /// <summary>
    /// Представляет задачи, принятые к исполнению.
    /// </summary>
    /// <typeparam name="TResult">Тип результата задачи</typeparam>
    public interface IMyTask<TResult>
    {
        /// <summary>
        /// Возможно применение supplier к результату предыдущей задачи 
        /// (к нему на вход результат предыдущей задачи). 
        /// Возвращает новую задачу, принятую на исполнение.
        /// </summary>
        /// <param name="supplier">Задача, представленная в виде вычисления</param>        
        IMyTask<TNewResult> ContinueWith<TNewResult>(Func<TResult, TNewResult> supplier); 

        /// <summary>
        /// Выполнена ли задача
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// Результат выполнения задачи
        /// </summary>
        TResult Result { get; }
    }
}
