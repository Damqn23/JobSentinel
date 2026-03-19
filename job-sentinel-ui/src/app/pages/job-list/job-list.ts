import { Component, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-job-list',
  standalone: true,
  imports: [],
  templateUrl: './job-list.html', 
  styleUrl: './job-list.css' 
})
export class JobListComponent {
  http = inject(HttpClient); 
  jobs: any[] = []; 

  constructor() {
    this.http.get<any[]>('http://localhost:5122/jobs').subscribe({
      next: (data) => this.jobs = data,
      error: (err) => console.error("Failed to load jobs", err)
    });
  }
}