using System;
using System.Linq;
using System.Windows;

namespace KinoWPF
{
    public partial class MainWindow : Window
    {
        // Статические поля для хранения сессии пользователя
        public static employee CurrentUser { get; set; }
        public static string Role { get; set; }
        public static bool IsAdmin => Role == "Администратор";

        public MainWindow()
        {
            InitializeComponent();

            // Вывод имени текущего пользователя
            if (CurrentUser != null)
                tb_UserName.Text = CurrentUser.Second_name + " " + CurrentUser.Name;
            else
                tb_UserName.Text = "—";

            // Скрытие модуля "Сотрудники" для не-администраторов
            if (!IsAdmin)
                btn_Employees.Visibility = Visibility.Collapsed;

            // Проверка на превышение бюджета при загрузке
            CheckBudgetExceeded();
        }

        // Метод проверки превышения бюджета
        private void CheckBudgetExceeded()
        {
            try
            {
                using (var db = new KinoEntities())
                {
                    // Формируем список проектов, где сумма затрат превышает бюджет
                    var overBudgetProjects = db.projects
                        .Where(p => p.Budget != null && p.Budget > 0) // Игнорируем проекты без указанного бюджета
                        .Select(p => new
                        {
                            ProjectName = p.Name,
                            Budget = p.Budget.Value,
                            // Считаем общую сумму затрат по проекту
                            TotalExpenses = p.expenses.Sum(e => e.Sum) ?? 0
                        })
                        .Where(p => p.TotalExpenses > p.Budget) // Отбираем только те, где затраты больше бюджета
                        .ToList();

                    // Если найдены проекты с превышением, показываем предупреждение
                    if (overBudgetProjects.Any())
                    {
                        string message = "Внимание! В следующих проектах сумма затрат превысила бюджет:\n\n";

                        foreach (var item in overBudgetProjects)
                        {
                            message += $"• {item.ProjectName}\n";
                            message += $"  Бюджет: {item.Budget:N0} руб.\n";
                            message += $"  Затраты: {item.TotalExpenses:N0} руб.\n\n";
                        }

                        MessageBox.Show(message, "Превышение бюджета",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при проверке бюджета: " + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EmployeesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsAdmin) { MessageBox.Show("Доступ запрещён."); return; }
            new EmployesWindow().ShowDialog();
        }

        private void ProjectsButton_Click(object sender, RoutedEventArgs e) => new ProjectsWindow().ShowDialog();
        private void LocationsButton_Click(object sender, RoutedEventArgs e) => new AreaWindow().ShowDialog();
        private void CrewButton_Click(object sender, RoutedEventArgs e) => new FilmCrewWindow().ShowDialog();
        private void ScheduleButton_Click(object sender, RoutedEventArgs e) => new ScheduleWindow().ShowDialog();
        private void CostsButton_Click(object sender, RoutedEventArgs e) => new ExpensesWindow().ShowDialog();
        private void RentalButton_Click(object sender, RoutedEventArgs e) => new RentalWindow().ShowDialog();

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentUser = null;
            Role = null;

            new AutorizationWindow().Show();
            this.Close();
        }
    }
}