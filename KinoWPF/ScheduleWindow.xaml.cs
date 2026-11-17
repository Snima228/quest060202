using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace KinoWPF
{
    public partial class ScheduleWindow : Window
    {
        private List<ScheduleDto> _all = new List<ScheduleDto>();
        private shooting_schedule _editing;

        public ScheduleWindow()
        {
            InitializeComponent();
            LoadLookups();
            LoadData();

            // Подписка на событие изменения даты в фильтре для мгновенной фильтрации
            dp_FilterDate.SelectedDateChanged += (s, ev) => ApplyFilter();
        }

        private void LoadLookups()
        {
            // Загрузка справочников проектов и площадок для выпадающих списков
            using (var db = new KinoEntities())
            {
                var projects = db.projects.ToList();
                cb_Project.ItemsSource = projects;
                cb_FilterProject.ItemsSource = projects;

                // Формирование списка площадок с полным адресом для удобства выбора
                cb_Area.ItemsSource = db.areas.Select(a => new
                {
                    a.Id,
                    Address = a.adress.street.city.Name + ", " +
                              a.adress.street.Name + ", д." + a.adress.Home
                }).ToList();
            }
        }

        private void LoadData()
        {
            try
            {
                using (var db = new KinoEntities())
                {
                    // Загрузка графика съемок и маппинг в DTO для таблицы
                    _all = db.shooting_schedule.Select(x => new ScheduleDto
                    {
                        Id = x.Id,
                        Date = x.Date,
                        Time = x.Time,
                        ProjectName = x.project.Name,
                        VenueAddress = x.area.adress.street.city.Name + ", " +
                                       x.area.adress.street.Name + ", д." +
                                       x.area.adress.Home,
                        ProjectId = x.Id_project,
                        AreaId = x.Id_area
                    }).ToList();
                }
                ApplyFilter();
            }
            catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
        }

        private void ApplyFilter()
        {
            // Фильтрация записей по выбранному проекту и конкретной дате
            var pid = cb_FilterProject.SelectedValue as int?;
            var dt = dp_FilterDate.SelectedDate;

            var q = _all.AsQueryable();
            if (pid != null) q = q.Where(x => x.ProjectId == pid);
            if (dt != null)
                q = q.Where(x => x.Date.HasValue &&
                                 x.Date.Value.Date == dt.Value.Date);

            dg_Schedule.ItemsSource = q.ToList();
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilter();

        private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
        {
            // Сброс фильтров и отображение всех записей
            cb_FilterProject.SelectedIndex = -1;
            dp_FilterDate.SelectedDate = null;
            ApplyFilter();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // Подготовка формы для добавления новой записи в график
            _editing = null;
            ClearForm();
            lbl_FormTitle.Text = "Новый съёмочный день";
            panelForm.Visibility = Visibility.Visible;
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var dto = dg_Schedule.SelectedItem as ScheduleDto;
            if (dto == null) { MessageBox.Show("Выберите запись."); return; }

            try
            {
                using (var db = new KinoEntities())
                {
                    // Поиск полной записи в базе данных по ID
                    _editing = db.shooting_schedule.FirstOrDefault(x => x.Id == dto.Id);
                    if (_editing == null) return;

                    // Заполнение формы текущими значениями
                    dp_Date.SelectedDate = _editing.Date;
                    tb_Time.Text = _editing.Time.ToString(@"hh\:mm");
                    cb_Project.SelectedValue = _editing.Id_project;
                    cb_Area.SelectedValue = _editing.Id_area;
                }
                lbl_FormTitle.Text = "Редактирование записи";
                panelForm.Visibility = Visibility.Visible;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var dto = dg_Schedule.SelectedItem as ScheduleDto;
            if (dto == null) { MessageBox.Show("Выберите запись."); return; }

            if (MessageBox.Show("Удалить запись?", "Подтверждение",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            try
            {
                using (var db = new KinoEntities())
                {
                    // Поиск и удаление записи из базы
                    var item = db.shooting_schedule.FirstOrDefault(x => x.Id == dto.Id);
                    if (item != null) db.shooting_schedule.Remove(item);
                    db.SaveChanges();
                }
                LoadData();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            lbl_ValidationError.Text = "";

            // Проверка заполнения обязательных полей
            if (dp_Date.SelectedDate == null)
            { lbl_ValidationError.Text = "Укажите дату."; return; }

            // Проверка корректности формата времени
            TimeSpan time;
            if (!TimeSpan.TryParse(tb_Time.Text, out time))
            { lbl_ValidationError.Text = "Время в формате ЧЧ:ММ."; return; }

            if (cb_Project.SelectedValue == null)
            { lbl_ValidationError.Text = "Выберите проект."; return; }

            if (cb_Area.SelectedValue == null)
            { lbl_ValidationError.Text = "Выберите площадку."; return; }

            try
            {
                using (var db = new KinoEntities())
                {
                    // Создание новой или обновление существующей записи
                    shooting_schedule s;
                    if (_editing == null)
                    { s = new shooting_schedule(); db.shooting_schedule.Add(s); }
                    else s = db.shooting_schedule.First(x => x.Id == _editing.Id);

                    // Присвоение значений из полей формы
                    s.Date = dp_Date.SelectedDate;
                    s.Time = time;
                    s.Id_project = (int)cb_Project.SelectedValue;
                    s.Id_area = (int)cb_Area.SelectedValue;
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

        private void ClearForm()
        {
            // Очистка полей формы
            dp_Date.SelectedDate = null;
            tb_Time.Text = "";
            cb_Project.SelectedIndex = -1;
            cb_Area.SelectedIndex = -1;
            lbl_ValidationError.Text = "";
        }
    }

    public class ScheduleDto
    {
        public int Id { get; set; }
        public DateTime? Date { get; set; }
        public TimeSpan Time { get; set; }
        public string ProjectName { get; set; }
        public string VenueAddress { get; set; }
        public int? ProjectId { get; set; }
        public int? AreaId { get; set; }
    }
}