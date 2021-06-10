import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { JoinComponent } from './join/join.component';
import { MainComponent } from './main/main.component';

const routes: Routes = [
  { path: 'join', component: JoinComponent },
  { path: 'main', component: MainComponent },
  /*{ path: '',   redirectTo: '/join', pathMatch: 'full' },
  { path: '**', component: JoinComponent}*/
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
