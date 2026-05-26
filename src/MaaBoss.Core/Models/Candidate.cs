using System.Collections.Generic;

namespace MaaBoss.Core.Models;

public class Candidate
{
    public string       Name              { get; set; } = "";
    public int          Age               { get; set; }
    public string       Gender            { get; set; } = "";
    public string       Experience        { get; set; } = "";
    public string       Education         { get; set; } = "";
    public string       CurrentCompany    { get; set; } = "";
    public string       CurrentPosition   { get; set; } = "";
    public string       SalaryExpectation { get; set; } = "";
    public string       Location          { get; set; } = "";
    public List<string> Skills            { get; set; } = [];
    public bool         IsNew             { get; set; }
    public string       ActiveStatus      { get; set; } = "";
}

public class CandidateDetail : Candidate
{
    public string               Phone          { get; set; } = "";
    public string               Email          { get; set; } = "";
    public List<WorkExperience> WorkExperience { get; set; } = [];
    public List<Project>        Projects       { get; set; } = [];
    public List<Education>      EducationList  { get; set; } = [];
    public string               SelfEvaluation { get; set; } = "";
    public string               JobStatus      { get; set; } = "";
    public string               ArrivalTime    { get; set; } = "";
}

public class WorkExperience
{
    public string Company { get; set; } = "";
    public string Position { get; set; } = "";
    public string Duration { get; set; } = "";
    public string Description { get; set; } = "";
}

public class Project
{
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string Description { get; set; } = "";
}

public class Education
{
    public string School { get; set; } = "";
    public string Major { get; set; } = "";
    public string Degree { get; set; } = "";
    public string Duration { get; set; } = "";
}
