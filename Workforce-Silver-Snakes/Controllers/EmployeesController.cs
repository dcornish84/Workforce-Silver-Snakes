﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Workforce_Silver_Snakes.Models;
using Workforce_Silver_Snakes.Models.ViewModels;

namespace Workforce_Silver_Snakes.Controllers
{
    public class EmployeesController : Controller
    {
        private readonly IConfiguration _config;
        public EmployeesController(IConfiguration config)
        {
            _config = config;
        }
        public SqlConnection Connection
        {
            get
            {
                return new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            }
        }

        // GET: Employees
        public ActionResult Index()
        {
            using (SqlConnection conn = Connection)
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT e.Id, e.FirstName, e.LastName, e.DepartmentId, d.[Name]
                                        FROM Employee e
                                        LEFT JOIN Department d ON e.DepartmentId = d.Id
                                        ";


                    var reader = cmd.ExecuteReader();
                    var employees = new List<Employee>();
                    while (reader.Read())
                    {
                        employees.Add(new Employee
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id")),
                            FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                            LastName = reader.GetString(reader.GetOrdinal("LastName")),
                            DepartmentId = reader.GetInt32(reader.GetOrdinal("DepartmentId")),
                            Department = new Department
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("DepartmentId")),
                                Name = reader.GetString(reader.GetOrdinal("Name"))
                            }
                        });
                    }
                    reader.Close();
                    return View(employees);
                }

            }
        }

        // GET: Employees/Details/5
        public ActionResult Details(int id)
        {
            var trainings = GetAllTrainingPrograms().Select(t => new TrainingSelect
            {
                Name = t.Name,
                Id = t.Id,
                isSelected = false
            }).ToList();

            using (SqlConnection conn = Connection)
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT e.Id AS EmployeeId, e.FirstName,e.LastName, 
                                        d.Id AS DepartmentId, d.[Name] AS DepartmentName, e.ComputerId, e.Email,
                                        t.Id AS TrainingProgramId, t.[Name] AS TrainingProgramName, c.Model,
                                        et.Id AS EmployeeTrainingId
                                       FROM Employee e
                                       LEFT JOIN Department d ON e.DepartmentId = d.Id
                                       LEFT JOIN Computer c ON e.ComputerId = c.Id
                                       LEFT JOIN EmployeeTraining et ON e.Id = et.EmployeeId
                                       LEFT JOIN TrainingProgram t ON t.Id = et.TrainingProgramId
                                       WHERE e.Id = @id";

                    cmd.Parameters.Add(new SqlParameter("@id", id));

                    var reader = cmd.ExecuteReader();
                    Employee employee = null;
                    while (reader.Read())

                    {
                        if (employee == null)
                        {
                            employee = new Employee
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                                FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                                LastName = reader.GetString(reader.GetOrdinal("LastName")),
                                Email = reader.GetString(reader.GetOrdinal("Email")),
                                Department = new Department(),
                                Computer = new Computer()

                            };
                            if (!reader.IsDBNull(reader.GetOrdinal("TrainingProgramName")))
                            {
                                employee.TrainingPrograms.Add(reader.GetString(reader.GetOrdinal("TrainingProgramName")));
                            }
                        }
                        else if (!reader.IsDBNull(reader.GetOrdinal("TrainingProgramName")))
                        {
                            employee.TrainingPrograms.Add(reader.GetString(reader.GetOrdinal("TrainingProgramName")));
                        }
                        if (employee.Department != null)
                        {
                            employee.Department = new Department
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("DepartmentId")),
                                Name = reader.GetString(reader.GetOrdinal("DepartmentName"))
                            };
                        }
                        if (employee.Computer != null)
                        {
                            employee.Computer = new Computer
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("ComputerId")),
                                Model = reader.GetString(reader.GetOrdinal("Model"))
                            };
                        }
                    };
                    reader.Close();

                    if (employee == null)
                    {
                        return NotFound($"No Employee found with the ID of {id}");
                    }
                    foreach (TrainingSelect training in trainings)
                    {
                        if (employee.TrainingPrograms.Any(e => e == training.Name))
                        {
                            training.isSelected = true;
                        }
                    }

                    employee.TrainingList = trainings;

                    return View(employee);
                }

            }
        }

        // POST: Employees/EditTraining/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditTraining(int id, Employee employee)
        {
            try
            {
                DeleteAllUpcomingTrianing(id);
                using (SqlConnection conn = Connection)
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        foreach (TrainingSelect training in employee.TrainingList)
                        {
                            if (training.isSelected)
                            {
                                cmd.CommandText = @"INSERT INTO EmployeeTraining (TrainingProgramId, EmployeeId)
                                            OUTPUT INSERTED.id
                                            VALUES (@trainigProgramId, @employeeId)";

                                cmd.Parameters.Add(new SqlParameter("@trainigProgramId", training.Id));
                                cmd.Parameters.Add(new SqlParameter("@employeeId", employee.Id));



                                cmd.ExecuteNonQuery();
                                cmd.Parameters.Clear();

                            }

                        }
                    }
                }

                // RedirectToAction("Index");
                return RedirectToAction(nameof(Details), new { id = id });
            }
            catch (Exception ex)
            {
                return RedirectToAction(nameof(Index));
            }
        }


        // GET: Employees/Create
        public ActionResult Create()
        {
            var departments = GetDepartments().Select(d => new SelectListItem
            {
                Text = d.Name,
                Value = d.Id.ToString()
            }).ToList();
            var computers = GetAvalibleComputers().Select(computers => new SelectListItem
            {
                Text = computers.Make + "" + computers.Model,
                Value = computers.Id.ToString()
            }).ToList();

            var viewModel = new EmployeeViewModel
            {
                Employee = new Employee(),
                Departments = departments,
                Computers = computers
            };
            if (computers.Count == 0)
            {
                return NotFound();
            }
            return View(viewModel);

        }

        // POST: Employees/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Employee employee)
        {
            try
            {
                using (SqlConnection conn = Connection)
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"INSERT INTO Employee 
                                            (FirstName, LastName, DepartmentId, IsSupervisor, ComputerId, Email)
                                            VALUES (@firstName, @lastName, @departmentId, @isSupervisor, @computerId, @email)";

                        cmd.Parameters.Add(new SqlParameter("@firstName", employee.FirstName));
                        cmd.Parameters.Add(new SqlParameter("@lastName", employee.LastName));
                        cmd.Parameters.Add(new SqlParameter("@departmentId", employee.DepartmentId));
                        cmd.Parameters.Add(new SqlParameter("@isSupervisor", employee.IsSupervisor));
                        cmd.Parameters.Add(new SqlParameter("@computerId", employee.ComputerId));
                        cmd.Parameters.Add(new SqlParameter("@email", employee.Email));

                        //use an excute non query for inserts bc we are not asking for anything back.
                        //it is a non query- shows that the rows were affected.
                        cmd.ExecuteNonQuery();
                    }
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                return View();
            }
        }

        // GET: Employees/Edit/5
        public ActionResult Edit(int id)
        {
            var departments = GetDepartments().Select(d => new SelectListItem
            {
                Text = d.Name,
                Value = d.Id.ToString()
            }).ToList();
            var computers = GetAvalibleComputers().Select(d => new SelectListItem
            {
                Text = d.Make,
                Value = d.Id.ToString()
            }).ToList();

            using (SqlConnection conn = Connection)
            {

                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT e.Id AS EmployeeId, e.FirstName, e.LastName, e.DepartmentId, e.Email, e.ComputerId
                                        FROM Employee e
                                        WHERE Id = @id";

                    cmd.Parameters.Add(new SqlParameter("@id", id));

                    var reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        var employee = new Employee
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                            FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                            LastName = reader.GetString(reader.GetOrdinal("LastName")),
                            DepartmentId = reader.GetInt32(reader.GetOrdinal("DepartmentId")),
                            Email = reader.GetString(reader.GetOrdinal("Email")),
                            ComputerId = reader.GetInt32(reader.GetOrdinal("ComputerId"))
                        };
                        reader.Close();

                        var viewModel = new EmployeeViewModel
                        {
                            Employee = employee,
                            Departments = departments,
                            Computers = computers
                        };
                        return View(viewModel);
                    }
                    reader.Close();
                    return NotFound();
                }

            }
        }


        // POST: Employees/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, Employee employee)
        {
            try
            {
                using (SqlConnection conn = Connection)
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"UPDATE Employee 
                                            SET 
                                            FirstName = @firstName, 
                                            LastName = @lastName, 
                                            DepartmentId = @departmentId,
                                            Email = @email,
                                            ComputerId = @computerId
                                            WHERE Id = @id";

                        cmd.Parameters.Add(new SqlParameter("@firstName", employee.FirstName));
                        cmd.Parameters.Add(new SqlParameter("@lastName", employee.LastName));
                        cmd.Parameters.Add(new SqlParameter("@departmentId", employee.DepartmentId));
                        cmd.Parameters.Add(new SqlParameter("@email", employee.Email));
                        cmd.Parameters.Add(new SqlParameter("@computerId", employee.ComputerId));
                        cmd.Parameters.Add(new SqlParameter("@id", id));

                        cmd.ExecuteNonQuery();
                    }
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                return RedirectToAction(nameof(Edit), new { id = id });
            }
        }

        // GET: Employees/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: Employees/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                // TODO: Add delete logic here

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // Helper method to get all departments
        private List<Department> GetDepartments()
        {
            using (SqlConnection conn = Connection)
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Id,[Name] FROM Department";
                    var reader = cmd.ExecuteReader();

                    var departments = new List<Department>();

                    while (reader.Read())
                    {
                        departments.Add(new Department
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id")),
                            Name = reader.GetString(reader.GetOrdinal("Name"))
                        });
                    }
                    reader.Close();
                    return departments;
                }

            }
        }

        //helper function to grab all training programs
        private List<TrainingProgram> GetAllTrainingPrograms()
        {
            using (SqlConnection conn = Connection)
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT Id, [Name], StartDate, EndDate, MaxAttendees 
                                        FROM TrainingProgram
                                        WHERE StartDate >= @today";
                    //
                    cmd.Parameters.Add(new SqlParameter("@today", DateTime.Now));
                    SqlDataReader reader = cmd.ExecuteReader();
                    List<TrainingProgram> trainingPrograms = new List<TrainingProgram>();

                    int IdOrdinal = reader.GetOrdinal("Id");
                    int NameOrdinal = reader.GetOrdinal("Name");
                    int StartDateOrdinal = reader.GetOrdinal("StartDate");
                    int EndDateOrdinal = reader.GetOrdinal("EndDate");
                    int MaxAttendeesOrdinal = reader.GetOrdinal("MaxAttendees");

                    while (reader.Read())
                    {
                        TrainingProgram trainingProgram = new TrainingProgram
                        {
                            Id = reader.GetInt32(IdOrdinal),
                            Name = reader.GetString(NameOrdinal),
                            StartDate = reader.GetDateTime(StartDateOrdinal),
                            EndDate = reader.GetDateTime(EndDateOrdinal),
                            MaxAttendees = reader.GetInt32(MaxAttendeesOrdinal)
                        };


                        trainingPrograms.Add(trainingProgram);
                    }
                    reader.Close();

                    return trainingPrograms;
                }
            }
        }

        //helper function to delete all upcoming trainings for employee
        private void DeleteAllUpcomingTrianing(int id)
        {
            try
            {
                using (SqlConnection conn = Connection)
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"DELETE et FROM EmployeeTraining et
                                            LEFT JOIN TrainingProgram tp ON tp.Id = et.TrainingProgramId
                                            WHERE EmployeeId = @eId AND StartDate >= @today";
                        cmd.Parameters.Add(new SqlParameter("@eId", id));
                        cmd.Parameters.Add(new SqlParameter("@today", DateTime.Now));

                        cmd.ExecuteNonQuery();

                    }
                }
            }
            catch (Exception)
            {

                throw;

            }
        }

        // Helper method to get all avalible computers
        private List<Computer> GetAvalibleComputers()
        {
            using (SqlConnection conn = Connection)
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT c.Id AS ComputerId, c.Make, 
                                        c.Model, c.PurchaseDate, 
                                        c.DecomissionDate
                                        FROM Computer c
                                        WHERE NOT EXISTS
                                        (SELECT e.ComputerId AS EmployeeComputerId
                                        FROM Employee e
                                        WHERE e.ComputerId = c.Id)";
                    var reader = cmd.ExecuteReader();
                    var computers = new List<Computer>();

                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(reader.GetOrdinal("ComputerId")))
                        {
                            int computerId = reader.GetInt32(reader.GetOrdinal("ComputerId"));
                            if (!computers.Any(c => c.Id == computerId))
                            {
                                Computer computer = new Computer
                                {
                                    Id = reader.GetInt32(reader.GetOrdinal("ComputerId")),
                                    Make = reader.GetString(reader.GetOrdinal("Make")),
                                    Model = reader.GetString(reader.GetOrdinal("Model"))
                                };
                                computers.Add(computer);
                            }
                        }
                    }

                    reader.Close();
                    return computers;
                }
            }
        }
    }
}
