using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PyramidalSumm
{
    public partial class MainWindow : Window
    {
        public SeriesCollection Series { get; set; }
        public List<string> AxisXLabels { get; set; } = new List<string>();

        public MainWindow()
        {
            InitializeComponent();
            Series = new SeriesCollection();
            chart.Series = Series;

            for (int i = 2; i <= Environment.ProcessorCount; i++)
            {
                comboBoxThreadCount.Items.Add(i);
            }

            comboBoxThreadCount.SelectedIndex = 0; // Устанавливаем значение по умолчанию на 2 потока
        }

        private async void btnRun_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput())
            {
                MessageBox.Show("Пожалуйста, заполните все поля корректными значениями.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int selectedThreadCount = (int)comboBoxThreadCount.SelectedItem;
            int startValue = int.Parse(textBoxStartValue.Text);
            int step = int.Parse(textBoxStep.Text);
            int maxValue = int.Parse(textBoxMaxValue.Text);

            await PerformBenchmark(selectedThreadCount, startValue, step, maxValue);
        }

        private bool ValidateInput()
        {
            // Проверяем, что текстовые поля не пустые и содержат только числа
            return !string.IsNullOrWhiteSpace(textBoxStartValue.Text) &&
                   !string.IsNullOrWhiteSpace(textBoxStep.Text) &&
                   !string.IsNullOrWhiteSpace(textBoxMaxValue.Text) &&
                   int.TryParse(textBoxStartValue.Text, out _) &&
                   int.TryParse(textBoxStep.Text, out _) &&
                   int.TryParse(textBoxMaxValue.Text, out _);
        }

        private async Task PerformBenchmark(int selectedThreadCount, int startValue, int step, int maxValue)
        {
            AxisXLabels.Clear();
            Series.Clear();
            outputText.Text = ""; // Очистка содержимого TextBlock

            var syncTimes = new ChartValues<double>();
            var taskTimes = new ChartValues<double>();
            var threadTimes = new ChartValues<double>();
            var output = "";

            double totalSyncTime = 0;
            double totalTaskTime = 0;
            double totalThreadTime = 0;

            for (int i = startValue; i <= maxValue; i += step)
            {
                Random random = new Random();
                List<long> numbers = Enumerable.Range(0, i).Select(_ => (long)random.Next(0, 100)).ToList();

                // Синхронное время
                var syncTime = MeasureExecutionTime(() => SyncSum(numbers));
                syncTimes.Add(syncTime);
                totalSyncTime += syncTime; // Суммируем общее время
                //output += $"Количество элементов: {i}. Последовательный алгоритм - {syncTime} мс\n";

                // Параллельное время с Thread
                var threadTime = MeasureExecutionTime(() => ParallelSumWithThreads(numbers, selectedThreadCount));
                threadTimes.Add(threadTime);
                totalThreadTime += threadTime; // Суммируем общее время
                //output += $"{selectedThreadCount} потоков - Параллельный алгоритм Thread - {threadTime} мс\n";

                // Параллельное время с Task
                var taskTime = await MeasureExecutionTimeAsync(() => ParallelSumWithTasks(numbers, selectedThreadCount));
                taskTimes.Add(taskTime);
                totalTaskTime += taskTime; // Суммируем общее время
                //output += $"{selectedThreadCount} потоков - Параллельный алгоритм Task - {taskTime} мс\n";

                AxisXLabels.Add($"{i}");
            }

            Series.Add(new LineSeries
            {
                Title = "Синхронная сумма",
                Values = syncTimes,
            });

            Series.Add(new LineSeries
            {
                Title = "Параллельная сумма (Task)",
                Values = taskTimes,
            });

            Series.Add(new LineSeries
            {
                Title = "Параллельная сумма (Thread)",
                Values = threadTimes,
            });

            chart.AxisX[0].Labels = AxisXLabels;

            // Заполнение TextBlock финальными значениями
                output += $"Количество элементов: {maxValue}.\n" +
                          $"Синхронный алгоритм: {totalSyncTime} мс\n" +
                          $"{selectedThreadCount} потоков\n" +
                          $"Параллельный алгоритм Thread - {totalThreadTime} мс\n" +
                          $"Параллельный алгоритм Task - {totalTaskTime} мс\n\n";

            outputText.Text = output; // Заполнение TextBlock финальными результатами
        }

        public long SyncSum(List<long> numbers)
        {
            long sum = 0;
            foreach (var number in numbers)
            {
                sum += number;
            }
            return sum;
        }

        public long ParallelSumWithThreads(List<long> numbers, int threadCount)
        {
            long totalSum = 0;
            int rangeSize = (int)Math.Ceiling((double)numbers.Count / threadCount);
            Thread[] threads = new Thread[threadCount];
            long[] partialSums = new long[threadCount];

            for (int i = 0; i < threadCount; i++)
            {
                int start = i * rangeSize;
                int end = Math.Min(start + rangeSize, numbers.Count); // Защита от выхода за пределы списка

                int threadIndex = i; // Локальная переменная для захвата текущего индекса потока

                threads[i] = new Thread(() =>
                {
                    long sum = 0;
                    for (int j = start; j < end; j++)
                    {
                        sum += numbers[j];
                    }
                    partialSums[threadIndex] = sum; // Использование локального потока индекса
                });
                threads[i].Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            totalSum = partialSums.Sum();
            return totalSum;
        }

        public async Task<long> ParallelSumWithTasks(List<long> numbers, int threadCount)
        {
            long totalSum = 0;
            int rangeSize = numbers.Count / threadCount;
            Task<long>[] tasks = new Task<long>[threadCount];

            for (int i = 0; i < threadCount; i++)
            {
                int start = i * rangeSize;
                int end = (i == threadCount - 1) ? numbers.Count : start + rangeSize;
                tasks[i] = Task.Run(() =>
                {
                    long sum = 0;
                    for (int j = start; j < end; j++)
                    {
                        sum += numbers[j];
                    }
                    return sum;
                });
            }

            long[] partialSums = await Task.WhenAll(tasks);
            totalSum = partialSums.Sum();
            return totalSum;
        }

        private long MeasureExecutionTime(Action action)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            return stopwatch.ElapsedMilliseconds;
        }

        private async Task<long> MeasureExecutionTimeAsync(Func<Task<long>> func)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            await func();
            stopwatch.Stop();
            return stopwatch.ElapsedMilliseconds;
        }
    }
}
