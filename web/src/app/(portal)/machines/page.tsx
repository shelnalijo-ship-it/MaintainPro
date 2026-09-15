import { Suspense } from "react";
import { LoadingSkeleton } from "@/components/ui";
import { MachineList } from "@/features/machines/machine-list";
export default function MachinesPage() { return <Suspense fallback={<LoadingSkeleton rows={8} />}><MachineList /></Suspense>; }
