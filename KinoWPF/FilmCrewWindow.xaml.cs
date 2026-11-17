using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace KinoWPF
{
    public partial class FilmCrewWindow : Window
    {
        private List<CrewDto> _all = new List<CrewDto>();
        private film_crew _editing;

        public FilmCrewWindow()
        {
            InitializeComponent();
            LoadLookups();
            LoadData();
        }

        private void LoadLookups()
        {
            // Загрузка списков проектов и сотрудников для выпадающих полей
            using (var db = new KinoEntities())
            {
                cb_Project.ItemsSource = db.projects.ToList();
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
                    // Формирование списка записей съемочной группы для отображения в таблице
                    _all = db.film_crew.Select(x => new CrewDto
                    {
                        Id = x.Id,
                        ProjectName = x.project.Name,
                        EmployeeFullName = x.employee.Second_name + " " + x.employee.Name,
                        PostName = x.employee.post != null ? x.employee.post.Name : ""
                    }).ToList();
                }
                dg_Crew.ItemsSource = _all;
            }
            catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // Очистка полей и подготовка формы для добавления нового участника
            _editing = null;
            cb_Project.SelectedIndex = -1;
            cb_Employee.SelectedIndex = -1;
            lbl_FormTitle.Text = "Новый участник";
            panelForm.Visibility = Visibility.Visible;
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var dto = dg_Crew.SelectedItem as CrewDto;
            if (dto == null) { MessageBox.Show("Выберите запись."); return; }

            try
            {
                using (var db = new KinoEntities())
                {
                    // Поиск выбранной записи в базе данных
                    _editing = db.film_crew.FirstOrDefault(x => x.Id == dto.Id);
                    if (_editing == null) return;

                    // Заполнение формы текущими значениями проекта и сотрудника
                    cb_Project.SelectedValue = _editing.Id_project;
                    cb_Employee.SelectedValue = _editing.Id_employee;
                }
                lbl_FormTitle.Text = "Редактирование";
                panelForm.Visibility = Visibility.Visible;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var dto = dg_Crew.SelectedItem as CrewDto;
            if (dto == null) { MessageBox.Show("Выберите запись."); return; }

            if (MessageBox.Show("Удалить запись?", "Подтверждение",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            try
            {
                using (var db = new KinoEntities())
                {
                    // Поиск и удаление записи из таблицы
                    var item = db.film_crew.FirstOrDefault(x => x.Id == dto.Id);
                    if (item != null) db.film_crew.Remove(item);
                    db.SaveChanges();
                }
                LoadData();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            lbl_ValidationError.Text = "";

            // Проверка на то, что выбраны и проект, и сотрудник
            if (cb_Project.SelectedValue == null)
            { lbl_ValidationError.Text = "Выберите проект."; return; }

            if (cb_Employee.SelectedValue == null)
            { lbl_ValidationError.Text = "Выберите сотрудника."; return; }

            try
            {
                using (var db = new KinoEntities())
                {
                    // Создание новой или получение существующей записи для сохранения
                    film_crew fc;
                    if (_editing == null) { fc = new film_crew(); db.film_crew.Add(fc); }
                    else fc = db.film_crew.First(x => x.Id == _editing.Id);

                    // Присвоение значений из выпадающих списков
                    fc.Id_project = (int)cb_Project.SelectedValue;
                    fc.Id_employee = (int)cb_Employee.SelectedValue;
                    db.SaveChanges();
                }
                panelForm.Visibility = Visibility.Collapsed;
                LoadData();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            // Скрытие формы без сохранения изменений
            panelForm.Visibility = Visibility.Collapsed;
            _editing = null;
        }
    }

    public class CrewDto
    {
        public int Id { get; set; }
        public string ProjectName { get; set; }
        public string EmployeeFullName { get; set; }
        public string PostName { get; set; }
    }
}