import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams, HttpResponse } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { environment } from '../../env/environment';
import { IFlatProduct } from '../../features/models/flattenProduct';
import { IEditProduct } from '../../features/models/editProduct';
import { sunParams } from '../../features/models/paramOptions';
import { PaginatedResults} from '../../features/models/pagination';

// Define an interface matching your C# Product model properties

@Injectable({
  providedIn: 'root' // Available globally across all standalone components
})
export class AdminProductService {
  sunParams = new sunParams()
  httpOptions = {
    headers: new HttpHeaders({
      'Content-Type': 'application/json'
    })
  };
  flatProducts: IFlatProduct[] = []; // Store the fetched products here
  private http = inject(HttpClient);
  // Base API path matching your backend localhost port configuration
  private apiUrl = environment.baseUrl;
  PaginatedResult?:PaginatedResults<IFlatProduct[]> = new PaginatedResults<IFlatProduct[]>()
  // 1. Fetch a single product for the Edit View
  getProductById(id: string): Observable<IFlatProduct> {
    return this.http.get<IFlatProduct>(`${this.apiUrl}EditProducts/${id}`)
  }

getAllProducts(sun: sunParams): Observable<PaginatedResults<IFlatProduct[]>> {
  let params = new HttpParams(); 
  
  if (sun?.sort) {
    params = params.append('sort', sun.sort);
  }
  if (sun?.category) {
    params = params.append('Category', sun.category);
  }
  if (sun?.search) {
    params = params.append('Search', sun.search);
  }
  if (sun?.pageNumber !== undefined && sun?.pageSize !== undefined) {
    params = params.append('pageNumber', sun.pageNumber.toString());
    params = params.append('pageSize', sun.pageSize.toString());
  }

  return this.http.get<IFlatProduct[]>(`${this.apiUrl}EditProducts`, { observe: 'response', params: params })
    .pipe(
      map((response: HttpResponse<IFlatProduct[]>) => {
        const paginationHeader = response.headers.get('X-Pagination');
       
        // Ensure the class property is initialized just in case
        if (!this.PaginatedResult) {
          this.PaginatedResult = new PaginatedResults<IFlatProduct[]>();
        }

        if (paginationHeader) {
          this.PaginatedResult.Pagination = JSON.parse(paginationHeader);
        }
    
        // Assign the body, using a fallback to an empty array if response.body is null
        this.PaginatedResult.result = response.body || []; 
        
        // Return the populated class property so the Observable emits it
        return this.PaginatedResult;
      })
    );     
}

  // 2. Send form updates matching your controller's HttpPut("[HttpPut("UpdateProduct/{id}")]")
  updateProduct(id: string, productData: IEditProduct): Observable<any> {
    if (!productData.productImages || !Array.isArray(productData.productImages)) {
      productData.productImages = [];
    }
    return this.http.post(
      `${this.apiUrl}EditProducts/UpdateProduct/${id}`,
      productData,
      this.httpOptions
    );
  }

  publishProduct(id: string, markup?: number): Observable<unknown> {
    let params = new HttpParams();
    if (markup !== undefined) {
      params = params.set('markup', markup.toString());
    }
    return this.http.post(`${this.apiUrl}EditProducts/Publish/${id}`, {}, { ...this.httpOptions, params });
  }

  publishBulk(productIds: string[], markup?: number): Observable<unknown[]> {
    return this.http.post<unknown[]>(`${this.apiUrl}EditProducts/PublishBulk`, {
      productIds,
      markupMultiplier: markup ?? 1.4,
    }, this.httpOptions);
  }

  getSettings(): Observable<{ defaultMarkup: number }> {
    return this.http.get<{ defaultMarkup: number }>(`${this.apiUrl}EditProducts/settings`);
  }
}
