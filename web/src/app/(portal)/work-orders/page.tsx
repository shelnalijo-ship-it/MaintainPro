import { Suspense } from "react";
import { LoadingSkeleton } from "@/components/ui";
import { WorkOrderList } from "@/features/work-orders/work-order-list";
export default function WorkOrdersPage(){return <Suspense fallback={<LoadingSkeleton rows={8}/>}><WorkOrderList/></Suspense>;}
