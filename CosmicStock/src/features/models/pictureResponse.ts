export interface IPicture {
  pictureId: string;
  photoUrl: string;
  productId: string; // Foreign key to the product
}