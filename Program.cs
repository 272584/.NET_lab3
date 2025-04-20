using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MatrixMultiplication
{
    public class Matrix
    {
        private readonly double[,] _data;
        public int Rows { get; }
        public int Cols { get; }

        public Matrix(int rows, int cols)
        {
            Rows = rows;
            Cols = cols;
            _data = new double[rows, cols];
        }

        public double this[int row, int col]
        {
            get => _data[row, col];
            set => _data[row, col] = value;
        }

        public void RandomFill(int min = 0, int max = 100)
        {
            var random = new Random();
            for (int i = 0; i < Rows; i++)
            {
                for (int j = 0; j < Cols; j++)
                {
                    _data[i, j] = random.Next(min, max);
                }
            }
        }

        public void Print()
        {
            for (int i = 0; i < Rows; i++)
            {
                for (int j = 0; j < Cols; j++)
                {
                    Console.Write($"{_data[i, j]:F0}\t");
                }
                Console.WriteLine();
            }
        }
    }

    public class MatrixMultiplier
    {
        public static Matrix MultiplySequential(Matrix a, Matrix b)
        {
            if (a.Cols != b.Rows)
                throw new ArgumentException("Invalid matrix dimensions for multiplication");

            var result = new Matrix(a.Rows, b.Cols);

            for (int i = 0; i < a.Rows; i++)
            {
                for (int j = 0; j < b.Cols; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < a.Cols; k++)
                    {
                        sum += a[i, k] * b[k, j];
                    }
                    result[i, j] = sum;
                }
            }

            return result;
        }

        public static Matrix MultiplyParallel(Matrix a, Matrix b, int maxThreads = 1)
        {
            if (a.Cols != b.Rows)
                throw new ArgumentException("Invalid matrix dimensions for multiplication");

            var result = new Matrix(a.Rows, b.Cols);
            var options = new ParallelOptions { MaxDegreeOfParallelism = maxThreads };

            Parallel.For(0, a.Rows, options, i =>
            {
                for (int j = 0; j < b.Cols; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < a.Cols; k++)
                    {
                        sum += a[i, k] * b[k, j];
                    }
                    result[i, j] = sum;
                }
            });

            return result;
        }
    }
    
    // Klasa pomocnicza przechowująca dane zadania dla wątku
    public class ThreadData
    {
        public Matrix? A { get; set; }
        public Matrix? B { get; set; }
        public Matrix? Result { get; set; }
        public int StartRow { get; set; }
        public int EndRow { get; set; }
    }

    

    // Dodajemy metodę mnożenia przy użyciu klasy Thread
    public static class ThreadMatrixMultiplier
    {
        public static Matrix MultiplyWithThreads(Matrix a, Matrix b, int numThreads)
        {
            if (a.Cols != b.Rows)
                throw new ArgumentException("Invalid matrix dimensions for multiplication");

            var result = new Matrix(a.Rows, b.Cols);
            var threads = new Thread[numThreads];
            int rowsPerThread = a.Rows / numThreads;
            
            // Tworzymy i uruchamiamy wszystkie wątki
            for (int i = 0; i < numThreads; i++)
            {
                int startRow = i * rowsPerThread;
                int endRow = (i == numThreads - 1) ? a.Rows : (i + 1) * rowsPerThread;
                
                var threadData = new ThreadData
                {
                    A = a,
                    B = b,
                    Result = result,
                    StartRow = startRow,
                    EndRow = endRow
                };
                
                threads[i] = new Thread(MultiplyPartialMatrix);
                threads[i].Name = $"Thread-{i+1}";
                threads[i].Start(threadData);
            }
            
            // Czekamy na zakończenie wszystkich wątków
            foreach (var thread in threads)
            {
                thread.Join();
            }
            
            return result;
        }
        
        private static void MultiplyPartialMatrix(object? data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
                
            var threadData = (ThreadData)data!;
            
            for (int i = threadData.StartRow; i < threadData.EndRow; i++)
            {
                for (int j = 0; j < threadData.B.Cols; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < threadData.A.Cols; k++)
                    {
                        sum += threadData.A[i, k] * threadData.B[k, j];
                    }
                    threadData.Result[i, j] = sum;
                }
            }
        }
    }
    
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Matrix Multiplication Performance Test");
            Console.WriteLine("=====================================");

            int[] matrixSizes = { 500, 750, 1000 };
            int[] threadCounts = { 1, 2, 4, 8, Environment.ProcessorCount };
            int repetitions = 3;

            foreach (int size in matrixSizes)
            {
                Console.WriteLine($"\nTesting for matrix size: {size}x{size}");
                Console.WriteLine("-------------------------------------");

                var a = new Matrix(size, size);
                var b = new Matrix(size, size);
                a.RandomFill();
                b.RandomFill();

                var sequentialTime = MeasureTime(() => MatrixMultiplier.MultiplySequential(a, b), repetitions);
                Console.WriteLine($"Sequential time: {sequentialTime} ms");

                Console.WriteLine("\nUsing Parallel.For:");
                foreach (int threads in threadCounts)
                {
                    var parallelTime = MeasureTime(() => MatrixMultiplier.MultiplyParallel(a, b, threads), repetitions);
                    var speedup = (double)sequentialTime / parallelTime;
                    Console.WriteLine($"Parallel ({threads} threads): {parallelTime} ms | Speedup: {speedup:F2}x");
                }
                
                Console.WriteLine("\nUsing Thread class:");
                foreach (int threads in threadCounts)
                {
                    var threadTime = MeasureTime(() => ThreadMatrixMultiplier.MultiplyWithThreads(a, b, threads), repetitions);
                    var speedup = (double)sequentialTime / threadTime;
                    Console.WriteLine($"Thread ({threads} threads): {threadTime} ms | Speedup: {speedup:F2}x");
                }
            }
        }

        static long MeasureTime(Action action, int repetitions)
        {
            // Rozgrzewka
            action();

            var stopwatch = new Stopwatch();
            long totalTime = 0;

            for (int i = 0; i < repetitions; i++)
            {
                stopwatch.Restart();
                action();
                stopwatch.Stop();
                totalTime += stopwatch.ElapsedMilliseconds;
            }

            return totalTime / repetitions;
        }
    }
}