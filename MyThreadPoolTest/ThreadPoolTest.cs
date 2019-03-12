namespace MyThreadPoolTest
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using MyThreadPool;
    using System;
    using System.Diagnostics;
    using System.Threading;

    [TestClass]
    public class ThreadPoolTest
    {
        private MyThreadPool.ThreadPool threadPool;        

        [TestCleanup]
        public void Clean()
        {            
            threadPool.Shutdown();                
        }

        [TestMethod]
        public void CanSolveTaskByOneThread()
        {
            threadPool = new MyThreadPool.ThreadPool(1);
            var task = threadPool.AddTask(() =>
            {
                Thread.Sleep(250);
                return 5;
            });

            Assert.AreEqual(5, task.Result);
        }

        [TestMethod]
        public void CanSolveTasksByOneThread()
        {
            threadPool = new MyThreadPool.ThreadPool(1);
            var tasks = new IMyTask<int>[10];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = threadPool.AddTask(() =>
                {
                    Thread.Sleep(250);
                    return 5;
                });
            }

            var result = 0;
            for (int i = 0; i < 10; i++)
            {
                result += tasks[i].Result;
            }
            Assert.AreEqual(50, result);
        }

        [TestMethod]
        public void CanSolveTask()
        {
            threadPool = new MyThreadPool.ThreadPool(4);
            var task = threadPool.AddTask(() => 5);
            Assert.AreEqual(5, task.Result);
        }       

        [TestMethod]
        public void ContinueWithSeveralTime_FromContinueWith_FromContWith_E_T_C()
        {
            threadPool = new MyThreadPool.ThreadPool(4);
            var t1 = threadPool.AddTask(() => 
            {                
                return 2;
            });
            var t2 = t1.ContinueWith(res => res * res);
            var t3 = t2.ContinueWith(res => res * res);
            var t4 = t3.ContinueWith(res => res * res);
            var res_t4 = t4.Result;
            Assert.AreEqual(256, res_t4);
        }

        [TestMethod]
        public void ContinueWithSeveralTimeWithWaitingInCalc_FromContinueWith_FromContWith_E_T_C()
        {
            threadPool = new MyThreadPool.ThreadPool(10);
            var t1 = threadPool.AddTask(() =>
            {
                Thread.Sleep(1000);
                return 2;
            });
            var t2 = t1.ContinueWith(res => { Thread.Sleep(1000); return res * res; } );
            var t3 = t2.ContinueWith(res => { Thread.Sleep(1000); return res * res; } );
            var t4 = t3.ContinueWith(res => { Thread.Sleep(1000); return res * res; } );
            var res_t4 = t4.Result;
            Assert.AreEqual(256, res_t4);
        }

        [TestMethod]
        [ExpectedException(typeof(AggregateException))]
        public void TaskOfContinueWithAfterShutDownShouldThrowException()
        {
            threadPool = new MyThreadPool.ThreadPool(4);
            var taskWhichWillContinue = threadPool.AddTask(() =>
            {
                var temp = 1;
                var result = 0;
                for (int i = 0; i < 1000; i++)
                {
                    temp += i;
                    result += i;
                }
                return result;
            });
            threadPool.Shutdown();
            var res = taskWhichWillContinue.ContinueWith(result => result + result);            
        }

        [TestMethod]
        public void ContinueWithTakeResultPreviouseLongTask()
        {         
            threadPool = new MyThreadPool.ThreadPool(4);
            var taskWhichWillContinue = threadPool.AddTask(() =>
            {
                var temp = 1;
                var result = 0;
                for (int i = 0; i < 1000; i++)
                {
                    temp += i;
                    result += i;
                }
                return result;
            });
            var res = taskWhichWillContinue.ContinueWith(result => result + result);

            Assert.AreEqual(999000, res.Result);            
        }

        [TestMethod]
        public void ContinueWithTakeResultPreviouseTaskWhichSleep()
        {
            threadPool = new MyThreadPool.ThreadPool(4);
            var taskWhichWillContinue = threadPool.AddTask(() =>
            {
                Thread.Sleep(4000);
                return 10;
            });
            var res = taskWhichWillContinue.ContinueWith(result => result + result);

            Assert.AreEqual(20, res.Result);
        }

        [TestMethod]
        public void ContinueWithSecondCallAfterOtherMethodWithContinueShouldNice()
        {
            threadPool = new MyThreadPool.ThreadPool(4);
            var task = threadPool.AddTask(() => 3);
            var newTask = task.ContinueWith(x => x + 5);

            Assert.AreEqual(8, newTask.Result);
        }

        [TestMethod]
        public void TaskCompleteddAndCouldBeContinue()
        {
            threadPool = new MyThreadPool.ThreadPool(4);
            var task = threadPool.AddTask(() => 5);

            Assert.AreEqual(25, task.ContinueWith(result => result * result).Result);
        }

        [TestMethod]
        public void ConstructorOfThreadPoolCompletedBeforeAddTask()
        {
            var numberThreads = 100;
            threadPool = new MyThreadPool.ThreadPool(numberThreads);
            Thread.Sleep(200);
            var temp = threadPool.AddTask(() => 5);
            
            Assert.AreEqual(numberThreads, threadPool.CountAliveThread());
        }                

        [TestMethod]
        public void CountAliveThreadBeforeShutDown()
        {
            var numberThreads = 4;
            threadPool = new MyThreadPool.ThreadPool(numberThreads);
            Thread.Sleep(3000);
            var realCount = threadPool.CountAliveThread();
            Assert.AreEqual(numberThreads, realCount);
        }

        [TestMethod]
        public void CountAliveThreadAfterShutDown()
        {
            var numberThreads = 10;
            threadPool = new MyThreadPool.ThreadPool(numberThreads);
            threadPool.Shutdown();
            Assert.AreEqual(0, threadPool.CountAliveThread());
        }        

        [TestMethod]
        public void ShouldWaitingToCompletedTaskAndGetResult()
        {
            threadPool = new MyThreadPool.ThreadPool(4);
            var task = threadPool.AddTask(() =>
                {
                    Thread.Sleep(5000);
                    return 5;
                });
            var result = task.Result;
            Assert.AreEqual(5, result);
        }

        [TestMethod]
        [ExpectedException(typeof(AggregateException))]
        public void ShouldThrowExceptionDuringTaskPerform()
        {
            Func<int> func = () =>
            {
                throw new Exception();
            };
            threadPool = new MyThreadPool.ThreadPool(1);
            var task = threadPool.AddTask(func);
            
            var shouldBeException = task.Result;
        }

        [TestMethod]
        [ExpectedException(typeof(AggregateException))]
        public void ShouldThrowExceptionAboutEndingOfThreadWorking()
        {
            threadPool = new MyThreadPool.ThreadPool(2);
            var task = threadPool.AddTask(() => 5);

            threadPool.Shutdown();
            var taskAfterShutDown = threadPool.AddTask(() => 5);
        }

        [TestMethod]
        public void NumberOfTaskMoreThatThreadsAndTheyWillCompleteAndGetResult()
        {
            var numberTasks = 5;
            var tasks = new IMyTask<int>[10];
            threadPool = new MyThreadPool.ThreadPool(4);
            for (int i = 0; i < numberTasks; i++)
            {
                tasks[i] = threadPool.AddTask(() =>
                {
                    Thread.Sleep(1500);
                    return 5;
                });
            }
            for (int i = 0; i < numberTasks; i++)
            {
                Assert.AreEqual(5, tasks[i].Result);
            }
        }                
        
        [TestMethod]
        public void PropertyIsCompletedOfTaskShouldBeCorrect()
        {            
            threadPool = new MyThreadPool.ThreadPool(4);
            var task = threadPool.AddTask(() => 5);

            var waitResult = task.Result;
            Assert.AreEqual(true, task.IsCompleted);                     
        }   

        [TestMethod]
        public void PropertyResultShouldWait()
        {
            threadPool = new MyThreadPool.ThreadPool(4);
            var task = threadPool.AddTask(() =>
            {
                long result = 1;
                for (long i = 0; i < 1000000; i++)
                {                  
                    result += i;
                }
                return 10;
            });            
            Assert.AreEqual(10, task.Result);
        }        

        [TestMethod]
        public void CanSolveManyTaskFastBy75ThreadsAndFinellyGetAllResult()
        {
            var numberOfTasksSoMuch = 500;                       

            threadPool = new MyThreadPool.ThreadPool(75);
            var tasks = new IMyTask<int>[numberOfTasksSoMuch];            
            for (int i = 0; i < tasks.Length - 10; i++)
            {
                tasks[i] = threadPool.AddTask(() =>
                {
                    Thread.Sleep(500);
                    return 5;
                });
            }
            for (int i = 0; i < 10; i++)
            {
                tasks[numberOfTasksSoMuch - 10  + i] = tasks[i].ContinueWith(x => x + x);
            }

            int result = 0;
            for (int i = 0; i < tasks.Length; i++)
            {
                result += tasks[i].Result;
            }

            Assert.AreEqual(2550, result);
        }                

        [TestMethod]
        public void ContinueWithDontBlockThreadWhichCalledContinueWith_TwoThreads()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            threadPool = new MyThreadPool.ThreadPool(2);                        
            var task = threadPool.AddTask(() =>
            {
                Thread.Sleep(3500);
                return 5;
            });           
            
            var newTask = task.ContinueWith(x => x + 5);
            
            var taskAfterContinueUnstopableTask = threadPool.AddTask(() => 5);
            var waitingResultOfLastTask = taskAfterContinueUnstopableTask.Result;

            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds > 3300)
            {
                Assert.Fail();
            }
        }
    }
}