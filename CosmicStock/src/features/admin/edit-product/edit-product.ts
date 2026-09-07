import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ReactiveFormsModule, FormGroup, FormArray, FormControl } from '@angular/forms';
import { AdminProductService } from '../../../core/services/admin-product';



@Component({
  selector: 'app-edit-product',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: '../edit-product/edit-product.html',
  styleUrls: ['../edit-product/edit-product.scss']
})
export class EditProductComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private productService = inject(AdminProductService); // Clean injection
  urlStrings!: string
  productId!: string;
  protected productForm: FormGroup = new FormGroup({});
  


  ngOnInit(): void {
  this.productId = this.route.snapshot.paramMap.get('id') || '';
  
  this.productForm = new FormGroup({
    id: new FormControl(this.productId),
    nameEn: new FormControl(''),
    descriptionEn: new FormControl(''),
    sku: new FormControl(''),
    isFeatured: new FormControl(false),
    isNewArrival: new FormControl(false),
    isTopSelling: new FormControl(false),
    sellPrice: new FormControl(''),
    category: new FormControl(''),
    bigImage: new FormControl(''),
    // Clean, empty FormArray tracking nested object groups
    productImages: new FormArray([])
  });

  console.log(this.productForm.value);
  this.getProduct();
  }

get productImagesArray(): FormArray {
  return this.productForm.get('productImages') as FormArray;
}


addImage(imageString: string): void {
  if (!imageString || !imageString.trim()) return;

  const imagesArray = this.productForm.get('productImages') as FormArray;

  // Build a distinct structural unit matching your template formControlNames
  const newImageGroup = new FormGroup({
    productId:new FormControl(''),
    photoUrl: new FormControl(imageString.trim()),
    skuPhoto: new FormControl(this.productForm.get('skuPhoto')?.value || '')
  });

  // Safely inject it into the reactive lifecycle tree
  imagesArray.push(newImageGroup);
  console.log(this.productForm.value);
}



  getProduct(){
      this.productService.getProductById(this.productId).subscribe({
      next: (product) => {
        const categoryName = typeof product.category === 'object' && product.category
          ? product.category.categoryName
          : '';

        this.productForm.patchValue({
          id: product.id,
          nameEn: product.nameEn,
          sku: product.sku,
          sellPrice: product.sellPrice,
          bigImage: product.bigImage ?? '',
          category: categoryName,
        });
      },
      error: (err) => console.error('Failed to load product metadata', err)
    });
  }

  onSubmit() {
    if (this.productForm.valid) {
      this.productService.updateProduct(this.productId, this.productForm.value).subscribe({
        next: (data) => console.log(data),
        error: (err) => console.error('Submit update failed', err)
      });
    }
  }
}
