using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace KinoWPF
{
    public partial class ExpensesWindow : Window
    {
        private List<ExpenseDto> _all = new List<ExpenseDto>();
        private expens _editing;

        public ExpensesWindow()
        {
            InitializeComponent();
            LoadLookups();
            LoadData();
        }

        private void LoadLookups()
        {
            // Загрузка справочников проектов, категорий затрат и сотрудников
            using (var db = new KinoEntities())
            {
                var projects = db.projects.ToList();
                cb_Project.ItemsSource = projects;
                cb_FilterProject.ItemsSource = projects;

                var cats = db.category_expense.ToList();
                cb_Category.ItemsSource = cats;
                cb_FilterCategory.ItemsSource = cats;

                cb_Employee.ItemsSource = db.employees
                    .Select(e => new
                    {
                        e.Id,
                        FullName = e.Second_name + " " + e.Name
                    }).ToList();
            }
        }

        private void LoadData()
        {
            try
            {
                using (var db = new KinoEntities())
                {
                    // Формирование списка затрат для таблицы
                    _all = db.expenses.Select(x => new ExpenseDto
                    {
                        Id = x.Id,
                        ProjectName = x.project.Name,
                        CategoryName = x.category_expense.Name,
                        EmployeeFullName = x.employee != null
                            ? x.employee.Second_name + " " + x.employee.Name : "",
                        Sum = x.Sum ?? 0,
                        Date = x.Date,
                        ProjectId = x.Id_project,
                        CategoryId = x.Id_category_expense
                    }).ToList();
                }
                ApplyFilter();
            }
            catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
        }

        private void ApplyFilter()
        {
            // Фильтрация по проекту и типу затраты
            var pid = cb_FilterProject.SelectedValue as int?;
            var cid = cb_FilterCategory.SelectedValue as int?;

            var q = _all.AsQueryable();
            if (pid != null) q = q.Where(x => x.ProjectId == pid);
            if (cid != null) q = q.Where(x => x.CategoryId == cid);

            dg_Expenses.ItemsSource = q.ToList();
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilter();

        private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
        {
            // Сброс фильтров
            cb_FilterProject.SelectedIndex = -1;
            cb_FilterCategory.SelectedIndex = -1;
            ApplyFilter();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // Подготовка формы для новой затраты
            _editing = null;
            ClearForm();
            lbl_FormTitle.Text = "Новая затрата";
            panelForm.Visibility = Visibility.Visible;
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var dto = dg_Expenses.SelectedItem as ExpenseDto;
            if (dto == null) { MessageBox.Show("Выберите запись."); return; }

            try
            {
                using (var db = new KinoEntities())
                {
                    // Получение записи из БД для редактирования
                    _editing = db.expenses.FirstOrDefault(x => x.Id == dto.Id);
                    if (_editing == null) return;

                    // Заполнение полей формы
                    cb_Project.SelectedValue = _editing.Id_project;
                    cb_Category.SelectedValue = _editing.Id_category_expense;
                    cb_Employee.SelectedValue = _editing.Id_employee;
                    tb_Sum.Text = (_editing.Sum ?? 0).ToString();
                    dp_Date.SelectedDate = _editing.Date;
                }
                lbl_FormTitle.Text = "Редактирование затраты";
                panelForm.Visibility = Visibility.Visible;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var dto = dg_Expenses.SelectedItem as ExpenseDto;
            if (dto == null) { MessageBox.Show("Выберите запись."); return; }

            if (MessageBox.Show("Удалить запись?", "Подтверждение",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            try
            {
                using (var db = new KinoEntities())
                {
                    // Поиск и удаление записи
                    var item = db.expenses.FirstOrDefault(x => x.Id == dto.Id);
                    if (item != null) db.expenses.Remove(item);
                    db.SaveChanges();
                }
                LoadData();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            lbl_ValidationError.Text = "";

            // Проверка обязательных полей
            if (cb_Project.SelectedValue == null)
            { lbl_ValidationError.Text = "Выберите проект."; return; }

            if (cb_Category.SelectedValue == null)
            { lbl_ValidationError.Text = "Выберите тип затраты."; return; }

            decimal sum;
            if (!decimal.TryParse(tb_Sum.Text, out sum) || sum <= 0)
            { lbl_ValidationError.Text = "Сумма больше нуля."; return; }

            if (dp_Date.SelectedDate == null)
            { lbl_ValidationError.Text = "Укажите дату."; return; }

            // Проверка, что дата не в прошлом
            DateTime expenseDate = dp_Date.SelectedDate.Value.Date;
            if (expenseDate < DateTime.Today)
            { lbl_ValidationError.Text = "Дата затраты не может быть в прошлом."; return; }

            try
            {
                using (var db = new KinoEntities())
                {
                    // Получаем выбранный проект для проверки даты его начала
                    int projectId = (int)cb_Project.SelectedValue;
                    var selectedProject = db.projects.FirstOrDefault(p => p.Id == projectId);

                    // Проверка, что дата затраты не раньше даты начала проекта
                    if (selectedProject != null && selectedProject.Date_start.HasValue)
                    {
                        if (expenseDate < selectedProject.Date_start.Value.Date)
                        {
                            lbl_ValidationError.Text = "Дата затраты не может быть раньше даты начала проекта.";
                            return;
                        }
                    }

                    // Создание или обновление записи
                    expens ex;
                    if (_editing == null) { ex = new expens(); db.expenses.Add(ex); }
                    else ex = db.expenses.First(x => x.Id == _editing.Id);

                    ex.Id_project = projectId;
                    ex.Id_category_expense = (int)cb_Category.SelectedValue;
                    ex.Id_employee = cb_Employee.SelectedValue as int?;
                    ex.Sum = sum;
                    ex.Date = dp_Date.SelectedDate;
                    db.SaveChanges();
                }
                panelForm.Visibility = Visibility.Collapsed;
                LoadData();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            // Скрытие формы без сохранения
            panelForm.Visibility = Visibility.Collapsed;
            _editing = null;
        }

        private void ClearForm()
        {
            // Сброс полей формы
            cb_Project.SelectedIndex = -1;
            cb_Category.SelectedIndex = -1;
            cb_Employee.SelectedIndex = -1;
            tb_Sum.Text = "";
            dp_Date.SelectedDate = null;
            lbl_ValidationError.Text = "";
        }
    }

    public class ExpenseDto
    {
        public int Id { get; set; }
        public string ProjectName { get; set; }
        public string CategoryName { get; set; }
        public string EmployeeFullName { get; set; }
        public decimal Sum { get; set; }
        public DateTime? Date { get; set; }
        public int? ProjectId { get; set; }
        public int? CategoryId { get; set; }
    }
}