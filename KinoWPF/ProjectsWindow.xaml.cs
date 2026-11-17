using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace KinoWPF
{
    public partial class ProjectsWindow : Window
    {
        private List<ProjectDto> _all = new List<ProjectDto>();
        private project _editing;

        public ProjectsWindow()
        {
            InitializeComponent();
            LoadStatuses();
            LoadData();
        }

        private void LoadStatuses()
        {
            // Загрузка статусов для фильтра и формы
            using (var db = new KinoEntities())
            {
                var list = db.status_project.ToList();
                cb_Status.ItemsSource = list;
                cb_FilterStatus.ItemsSource = list;
            }
        }

        private void LoadData()
        {
            try
            {
                using (var db = new KinoEntities())
                {
                    // Формирование списка проектов для таблицы
                    _all = db.projects.Select(x => new ProjectDto
                    {
                        Id = x.Id,
                        Name = x.Name,
                        Genre = x.genre_in_project.Select(g => g.genre.Name).FirstOrDefault(),
                        Budget = x.Budget ?? 0,
                        DateStart = x.Date_start,
                        DateEnd = x.Date_end,
                        TargetAudience = x.Target_audience,

                        Status = x.status_project != null ? x.status_project.Name : "",
                        StatusId = x.Id_status_project
                    }).ToList();
                }
                ApplyFilter();
            }
            catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
        }

        private void ApplyFilter()
        {
            // Фильтрация проектов по выбранному статусу
            int? sid = cb_FilterStatus.SelectedValue as int?;
            dg_Projects.ItemsSource = sid == null
                ? _all
                : _all.Where(x => x.StatusId == sid).ToList();
        }

        private void CbFilterStatus_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilter();

        private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
        {
            // Сброс фильтра и отображение всех проектов
            cb_FilterStatus.SelectedIndex = -1;
            ApplyFilter();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // Подготовка формы для создания нового проекта
            _editing = null;
            ClearForm();
            LoadGenres();
            lbl_FormTitle.Text = "Новый проект";
            panelForm.Visibility = Visibility.Visible;
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var dto = dg_Projects.SelectedItem as ProjectDto;
            if (dto == null) { MessageBox.Show("Выберите проект."); return; }

            try
            {
                using (var db = new KinoEntities())
                {
                    // Поиск выбранного проекта в базе
                    _editing = db.projects.FirstOrDefault(x => x.Id == dto.Id);
                    if (_editing == null) return;

                    LoadGenres();

                    // Заполнение формы текущими данными
                    tb_Name.Text = _editing.Name;
                    tb_Budget.Text = (_editing.Budget ?? 0).ToString();
                    dp_Start.SelectedDate = _editing.Date_start;
                    dp_End.SelectedDate = _editing.Date_end;
                    cb_Status.SelectedValue = _editing.Id_status_project;
                    tb_Audience.Text = _editing.Target_audience?.ToString();

                    var gId = _editing.genre_in_project
                        .Select(g => g.Id_genre).FirstOrDefault();
                    if (gId != null) cb_Genre.SelectedValue = (int)gId;
                }

                lbl_FormTitle.Text = "Редактирование проекта";
                panelForm.Visibility = Visibility.Visible;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var dto = dg_Projects.SelectedItem as ProjectDto;
            if (dto == null) { MessageBox.Show("Выберите проект."); return; }

            if (MessageBox.Show("Удалить проект?", "Подтверждение",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            try
            {
                using (var db = new KinoEntities())
                {
                    // Проверка наличия связанных записей перед удалением
                    if (db.expenses.Any(x => x.Id_project == dto.Id) ||
                        db.film_crew.Any(x => x.Id_project == dto.Id) ||
                        db.shooting_schedule.Any(x => x.Id_project == dto.Id) ||
                        db.rentals.Any(x => x.Id_project == dto.Id))
                    {
                        MessageBox.Show("Нельзя удалить: есть связанные записи.");
                        return;
                    }

                    // Удаление связей с жанрами
                    var links = db.genre_in_project.Where(x => x.Id_project == dto.Id).ToList();
                    db.genre_in_project.RemoveRange(links);

                    var p = db.projects.FirstOrDefault(x => x.Id == dto.Id);
                    if (p != null) db.projects.Remove(p);
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
            if (string.IsNullOrEmpty(tb_Name.Text))
            { lbl_ValidationError.Text = "Название обязательно."; return; }

            // Проверка числовых значений
            decimal budget;
            if (!decimal.TryParse(tb_Budget.Text, out budget) || budget <= 0)
            { lbl_ValidationError.Text = "Бюджет больше нуля."; return; }

            // Проверка заполнения дат
            if (dp_Start.SelectedDate == null)
            { lbl_ValidationError.Text = "Укажите дату начала."; return; }

            if (dp_End.SelectedDate == null)
            { lbl_ValidationError.Text = "Укажите дату окончания."; return; }

            // Проверка, что дата начала не в прошлом
            if (dp_Start.SelectedDate.Value.Date < DateTime.Today)
            { lbl_ValidationError.Text = "Дата начала не может быть в прошлом."; return; }

            // Проверка, что дата окончания не в прошлом
            if (dp_End.SelectedDate.Value.Date < DateTime.Today)
            { lbl_ValidationError.Text = "Дата окончания не может быть в прошлом."; return; }

            // Проверка логики дат
            if (dp_End.SelectedDate < dp_Start.SelectedDate)
            { lbl_ValidationError.Text = "Дата окончания не раньше даты начала."; return; }

            if (cb_Status.SelectedValue == null)
            { lbl_ValidationError.Text = "Выберите статус."; return; }

            int aud;
            int? audience = int.TryParse(tb_Audience.Text, out aud) ? (int?)aud : null;

            try
            {
                using (var db = new KinoEntities())
                {
                    // Создание нового или получение существующего проекта
                    project p;
                    if (_editing == null) { p = new project(); db.projects.Add(p); }
                    else p = db.projects.First(x => x.Id == _editing.Id);

                    // Присвоение значений из формы
                    p.Name = tb_Name.Text.Trim();
                    p.Budget = budget;
                    p.Date_start = dp_Start.SelectedDate;
                    p.Date_end = dp_End.SelectedDate;
                    p.Id_status_project = (int)cb_Status.SelectedValue;
                    p.Target_audience = audience;
                    db.SaveChanges();

                    // Сохранение выбранного жанра
                    var gid = cb_Genre.SelectedValue as int?;
                    if (gid.HasValue)
                    {
                        var oldLinks = db.genre_in_project
                            .Where(x => x.Id_project == p.Id).ToList();
                        db.genre_in_project.RemoveRange(oldLinks);
                        db.genre_in_project.Add(new genre_in_project
                        { Id_project = p.Id, Id_genre = gid });
                        db.SaveChanges();
                    }
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

        private void LoadGenres()
        {
            // Загрузка справочника жанров
            using (var db = new KinoEntities())
                cb_Genre.ItemsSource = db.genres.ToList();
        }

        private void ClearForm()
        {
            // Очистка полей формы
            tb_Name.Text = "";
            tb_Budget.Text = "";
            tb_Audience.Text = "";
            dp_Start.SelectedDate = null;
            dp_End.SelectedDate = null;
            cb_Status.SelectedIndex = -1;
            cb_Genre.SelectedIndex = -1;
            lbl_ValidationError.Text = "";
        }
    }

    public class ProjectDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Genre { get; set; }
        public decimal Budget { get; set; }
        public string Status { get; set; }
        public int? StatusId { get; set; }
        public DateTime? DateStart { get; set; }
        public DateTime? DateEnd { get; set; }
        public int? TargetAudience { get; set; }
    }
}