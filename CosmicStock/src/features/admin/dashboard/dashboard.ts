import { Component, inject, OnInit, signal } from '@angular/core';
import { environment } from '../../../env/environment';
import { IFlatProduct } from '../../models/flattenProduct';
import { RouterLink } from '@angular/router';
import { sunParams } from '../../models/paramOptions';
import { AdminProductService } from '../../../core/services/admin-product';
import { IPagination } from '../../models/pagination';


@Component({
  imports: [RouterLink],
  selector: 'app-dashboard',
  styleUrl: './dashboard.scss',
  templateUrl: './dashboard.html',
})
export class DashboardComponent implements OnInit{
  private sun:sunParams = new sunParams
  private adminService = inject(AdminProductService);
   p?:IPagination;
  
  protected readonly baseUrl =environment.baseUrl;
  protected readonly apiData = signal<IFlatProduct[]>([]);  

  ngOnInit() {
    this.getProductsToEdit()
  }

  getProductsToEdit(){
    this.adminService.getAllProducts(this.sun).subscribe({
      next:(response)=>{
        this.apiData.set(response.result??[])
        this.p = response.Pagination
      },
      error:(err)=>{
        console.error('Error get products', err)
      }
    })
  }

}
